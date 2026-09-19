using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

/// <summary>Parameter keys for the watercolor preset.</summary>
internal static class WatercolorParams
{
    public const string Wetness = "wetness";
    public const string Pigments = "pigments";
    public const string EdgePooling = "edgePooling";
    public const string PaperGrain = "paperGrain";
}

/// <summary>
/// Watercolor preset: wet edge-preserving washes, a limited pigment palette with soft
/// nearest-color blending, darker pigment pooling at boundaries, and paper grain.
/// </summary>
public sealed class WatercolorPreset : IStylePreset
{
    public const string PresetId = "builtin.watercolor";

    public string Id => PresetId;

    public string DisplayNameKey => "Preset.Watercolor.Name";

    public string DescriptionKey => "Preset.Watercolor.Description";

    public string? IconGlyph => "\uE753"; // Segoe MDL2 Wet (droplet)

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter(WatercolorParams.Wetness, "Param.Wetness", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter(WatercolorParams.Pigments, "Param.Pigments", 10, 3, 24, 1),
        new PresetParameter(WatercolorParams.EdgePooling, "Param.EdgePooling", 0.55, 0.0, 1.0, 0.05),
        new PresetParameter(WatercolorParams.PaperGrain, "Param.PaperGrain", 0.4, 0.0, 1.0, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[]
        {
            new WetStage(),
            new PigmentStage(),
            new EdgePoolingStage(),
            new PaperGrainStage(),
        };
}

/// <summary>
/// Wet stage: iterated separable bilateral-style smoothing with a wide range sigma —
/// merges gentle gradients into flat washes the way wet pigment settles.
/// </summary>
public sealed class WetStage : StageBase
{
    public override string Name => "Wet";

    public override async Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double wetness = Dbl(context.Parameters, WatercolorParams.Wetness, 0.6);
        if (wetness <= 0.01) return context;

        var src = context.Working!;
        int iterations = 1 + (int)Math.Round(wetness * 4);  // 1..5 passes
        int radius = 2 + (int)Math.Round(wetness * 4);      // 2..6 px
        double sigmaRange = 40;                              // keep washes, cut texture

        var a = src.Pixels;
        var b = new byte[a.Length];
        int width = src.Width, height = src.Height, stride = src.Stride;

        var rangeWeights = new double[256];
        double inv2sr2 = 1.0 / (2.0 * sigmaRange * sigmaRange);
        for (int d = 0; d < 256; d++) rangeWeights[d] = Math.Exp(-d * d * inv2sr2);

        for (int iter = 0; iter < iterations; iter++)
        {
            await Task.Run(() => Pass(a, b, width, height, stride, radius, rangeWeights, horizontal: true, cancellationToken), cancellationToken);
            await Task.Run(() => Pass(b, a, width, height, stride, radius, rangeWeights, horizontal: false, cancellationToken), cancellationToken);
            progress?.Report(new StageProgress(Name, 0, 1, (iter + 1) / (double)iterations));
        }

        return context;
    }

    private static void Pass(byte[] input, byte[] output, int width, int height, int stride,
        int radius, double[] rangeWeights, bool horizontal, CancellationToken ct)
    {
        Parallel.For(0, horizontal ? height : width, new ParallelOptions { CancellationToken = ct }, line =>
        {
            for (int p = 0; p < (horizontal ? width : height); p++)
            {
                int i = horizontal ? line * stride + p * 4 : p * stride + line * 4;
                int centerLuma = (input[i + 2] * 299 + input[i + 1] * 587 + input[i] * 114) / 1000;

                double sumB = 0, sumG = 0, sumR = 0, sumW = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = p + k;
                    if (q < 0 || q >= (horizontal ? width : height)) continue;
                    int j = horizontal ? line * stride + q * 4 : q * stride + line * 4;
                    int luma = (input[j + 2] * 299 + input[j + 1] * 587 + input[j] * 114) / 1000;
                    double w = rangeWeights[Math.Abs(luma - centerLuma)];
                    sumB += input[j] * w; sumG += input[j + 1] * w; sumR += input[j + 2] * w;
                    sumW += w;
                }

                output[i] = ClampByte(sumB / sumW);
                output[i + 1] = ClampByte(sumG / sumW);
                output[i + 2] = ClampByte(sumR / sumW);
                output[i + 3] = input[i + 3];
            }
        });
    }

    private static byte ClampByte(double v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
}

/// <summary>
/// Pigment stage: builds a small palette from an RGB(4,4,4) histogram (top counts with a
/// minimum distance so dark counts still win), then maps each pixel to its nearest
/// pigment with a soft blend — layered glazes instead of hard poster edges.
/// </summary>
public sealed class PigmentStage : StageBase
{
    public override string Name => "Pigment";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int pigments = Math.Clamp(Int(context.Parameters, WatercolorParams.Pigments, 10), 3, 32);
        var src = context.Working!;
        var px = src.Pixels;
        int stride = src.Stride;

        // histogram over 4-bit-per-channel keys; thread-local accumulation, merged once
        var counts = new int[4096];
        var sumB = new long[4096]; var sumG = new long[4096]; var sumR = new long[4096];

        Parallel.For(0, src.Height,
            new ParallelOptions { CancellationToken = cancellationToken },
            () => new HistAcc(),
            (y, _, local) =>
            {
                int row = y * stride;
                for (int x = 0; x < src.Width; x++)
                {
                    int i = row + x * 4;
                    int key = Key(px[i], px[i + 1], px[i + 2]);
                    local.Counts[key]++;
                    local.SumB[key] += px[i]; local.SumG[key] += px[i + 1]; local.SumR[key] += px[i + 2];
                }
                return local;
            },
            local =>
            {
                lock (counts)
                {
                    for (int k = 0; k < 4096; k++)
                    {
                        if (local.Counts[k] == 0) continue;
                        counts[k] += local.Counts[k];
                        sumB[k] += local.SumB[k]; sumG[k] += local.SumG[k]; sumR[k] += local.SumR[k];
                    }
                }
            });

        var candidates = Enumerable.Range(0, 4096)
            .Where(k => counts[k] > 0)
            .OrderByDescending(k => counts[k])
            .ToList();

        var palette = new List<(int B, int G, int R)>();
        foreach (var k in candidates)
        {
            if (palette.Count >= pigments) break;
            var c = (
                B: (int)(sumB[k] / counts[k]),
                G: (int)(sumG[k] / counts[k]),
                R: (int)(sumR[k] / counts[k]));
            bool tooClose = palette.Any(p =>
            {
                int db = p.B - c.B, dg = p.G - c.G, dr = p.R - c.R;
                return db * db + dg * dg + dr * dr < 48 * 48;
            });
            if (!tooClose) palette.Add(c);
        }
        if (palette.Count == 0) palette.Add((128, 128, 128));

        // nearest pigment per pixel, blended softly with the washed color
        double blend = 0.85;
        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < src.Width; x++)
            {
                int i = y * stride + x * 4;
                int b = px[i], g = px[i + 1], r = px[i + 2];

                int best = 0, bestDist = int.MaxValue;
                for (int p = 0; p < palette.Count; p++)
                {
                    var c = palette[p];
                    int db = c.B - b, dg = c.G - g, dr = c.R - r;
                    int d = db * db + dg * dg + dr * dr;
                    if (d < bestDist) { bestDist = d; best = p; }
                }
                var pick = palette[best];

                px[i] = PixelOps.Clamp((int)(b + (pick.B - b) * blend));
                px[i + 1] = PixelOps.Clamp((int)(g + (pick.G - g) * blend));
                px[i + 2] = PixelOps.Clamp((int)(r + (pick.R - r) * blend));
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }

    private static int Key(byte b, byte g, byte r) =>
        (b >> 4) << 8 | (g >> 4) << 4 | (r >> 4);

    private sealed class HistAcc
    {
        public readonly int[] Counts = new int[4096];
        public readonly long[] SumB = new long[4096];
        public readonly long[] SumG = new long[4096];
        public readonly long[] SumR = new long[4096];
    }
}

/// <summary>
/// Edge pooling: watercolor pigment gathers at wash boundaries — darkens pixels where
/// the luma gradient is strong, with a slight warm bias (cool channels sink faster).
/// </summary>
public sealed class EdgePoolingStage : StageBase
{
    public override string Name => "EdgePooling";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double strength = Dbl(context.Parameters, WatercolorParams.EdgePooling, 0.55);
        if (strength <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        var luma = new byte[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
                luma[y * width + x] = PixelOps.Luma(px, y * stride + x * 4);
        });

        Parallel.For(1, height - 1, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 1; x < width - 1; x++)
            {
                int gx =
                    -luma[(y - 1) * width + x - 1] - 2 * luma[y * width + x - 1] - luma[(y + 1) * width + x - 1]
                    + luma[(y - 1) * width + x + 1] + 2 * luma[y * width + x + 1] + luma[(y + 1) * width + x + 1];
                int gy =
                    -luma[(y - 1) * width + x - 1] - 2 * luma[(y - 1) * width + x] - luma[(y - 1) * width + x + 1]
                    + luma[(y + 1) * width + x - 1] + 2 * luma[(y + 1) * width + x] + luma[(y + 1) * width + x + 1];

                double t = Math.Sqrt(gx * gx + gy * gy) / 900.0;
                if (t < 0.18) continue;
                double pool = Math.Min(1.0, (t - 0.18) / 0.6) * strength;

                int i = y * stride + x * 4;
                px[i] = PixelOps.Clamp((int)(px[i] * (1.0 - pool * 0.55)));       // blue sinks most
                px[i + 1] = PixelOps.Clamp((int)(px[i + 1] * (1.0 - pool * 0.45)));
                px[i + 2] = PixelOps.Clamp((int)(px[i + 2] * (1.0 - pool * 0.40)));
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}

/// <summary>
/// Paper grain: deterministic hash noise plus faint fiber banding, multiplying the
/// washed color so the texture reads as cold-press paper.
/// </summary>
public sealed class PaperGrainStage : StageBase
{
    public override string Name => "PaperGrain";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double grain = Dbl(context.Parameters, WatercolorParams.PaperGrain, 0.4);
        if (grain <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        var px = src.Pixels;
        int stride = src.Stride;
        double noiseGain = grain * 0.16;
        double fiberGain = grain * 0.05;

        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < src.Width; x++)
            {
                int h = x * 374761393 + y * 668265263;
                h = (h ^ (h >> 13)) * 1274126177;
                double n = ((h ^ (h >> 16)) & 0xFFFF) / 65535.0 - 0.5;

                double fiber = Math.Sin(y * 1.7 + Math.Sin(x * 0.9) * 2.0);
                double factor = (1.0 + n * noiseGain) * (1.0 + fiber * fiberGain);

                int i = y * stride + x * 4;
                px[i] = PixelOps.Clamp((int)(px[i] * factor));
                px[i + 1] = PixelOps.Clamp((int)(px[i + 1] * factor));
                px[i + 2] = PixelOps.Clamp((int)(px[i + 2] * factor));
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
