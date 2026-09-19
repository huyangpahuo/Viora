using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

/// <summary>Parameter keys for the pencil sketch / line drawing preset.</summary>
internal static class SketchParams
{
    public const string LineThreshold = "lineThreshold";
    public const string LineThickness = "lineThickness";
    public const string Shading = "shading";
}

/// <summary>
/// Sketch preset: white paper, dark ink on strong luma edges (Sobel + hysteresis-free
/// threshold + dilation), optional soft graphite shading from a blurred luma map.
/// </summary>
public sealed class SketchPreset : IStylePreset
{
    public const string PresetId = "builtin.sketch";

    public string Id => PresetId;

    public string DisplayNameKey => "Preset.Sketch.Name";

    public string DescriptionKey => "Preset.Sketch.Description";

    public string? IconGlyph => "\uE70F"; // Segoe MDL2 Edit (pencil)

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter(SketchParams.LineThreshold, "Param.LineThreshold", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter(SketchParams.LineThickness, "Param.LineThickness", 1, 1, 4, 1),
        new PresetParameter(SketchParams.Shading, "Param.Shading", 0.35, 0.0, 1.0, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[]
        {
            new InkSketchStage(),
        };
}

/// <summary>
/// Ink sketch: renders the whole drawing in one pass — Sobel edges become ink lines,
/// blurred luma becomes faint graphite tone, everything else stays paper.
/// </summary>
public sealed class InkSketchStage : StageBase
{
    public override string Name => "InkSketch";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double detail = Dbl(context.Parameters, SketchParams.LineThreshold, 0.5);
        int thickness = Math.Clamp(Int(context.Parameters, SketchParams.LineThickness, 1), 1, 4);
        double shading = Dbl(context.Parameters, SketchParams.Shading, 0.35);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        // 1) Luma + Sobel magnitude (0..~1020).
        var luma = new byte[width * height];
        var mag = new float[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                luma[y * width + x] = PixelOps.Luma(px, y * stride + x * 4);
            }
        });
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int xm = Math.Max(0, x - 1), xp = Math.Min(width - 1, x + 1);
                int ym = Math.Max(0, y - 1), yp = Math.Min(height - 1, y + 1);
                int gx =
                    -luma[ym * width + xm] - 2 * luma[y * width + xm] - luma[yp * width + xm]
                    + luma[ym * width + xp] + 2 * luma[y * width + xp] + luma[yp * width + xp];
                int gy =
                    -luma[ym * width + xm] - 2 * luma[ym * width + x] - luma[ym * width + xp]
                    + luma[yp * width + xm] + 2 * luma[yp * width + x] + luma[yp * width + xp];
                mag[y * width + x] = (float)Math.Sqrt(gx * gx + gy * gy);
            }
        });

        // 2) Threshold into an edge map; lower threshold = more lines (stronger detail).
        //    Scale chosen so a ~40-luma step (common object boundary) at default detail
        //    still inks; the band below decides how quickly lines saturate.
        float threshold = (1f - (float)detail) * 240f + 40f;
        var edge = new byte[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                float e = (mag[y * width + x] - threshold) / 180f;
                edge[y * width + x] = e <= 0 ? (byte)0 : (byte)Math.Min(255, (int)(e * 255));
            }
        });

        // 3) Optional dilation (max filter, separable) for thicker strokes.
        if (thickness > 1)
        {
            MaxDilate(edge, width, height, thickness - 1, cancellationToken);
        }

        // 4) Graphite shading: inverted blurred luma, faint and only where requested.
        var blurred = (byte[])luma.Clone();
        BoxBlurGray(blurred, width, height, Math.Max(2, Math.Min(width, height) / 120), cancellationToken);

        // 5) Composite: paper × shading, ink on edges.
        const int paperB = 245, paperG = 248, paperR = 250; // warm white
        const int inkB = 42, inkG = 40, inkR = 38;

        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                float e = edge[idx] / 255f;
                float tone = (255f - blurred[idx]) / 255f * (float)shading * 0.55f;

                // deterministic per-pixel paper grain (hash, no per-pixel Random cost)
                int h = (x * 374761393 + y * 668265263) ^ ((x + 31) * 668265263);
                h = (h ^ (h >> 13)) * 1274126177;
                int n = ((h ^ (h >> 16)) & 0xFF) - 128;

                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                {
                    int paper = c == 0 ? paperB : c == 1 ? paperG : paperR;
                    int ink = c == 0 ? inkB : c == 1 ? inkG : inkR;
                    double v = paper + (ink - paper) * e;
                    v *= (1.0 - tone) * (1.0 + n / 255.0 * 0.04);
                    px[i + c] = PixelOps.Clamp((int)v);
                }
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }

    /// <summary>Separable max filter (dilation) on a single-channel byte map, in place.</summary>
    private static void MaxDilate(byte[] map, int width, int height, int radius, CancellationToken ct)
    {
        var tmp = new byte[map.Length];

        Parallel.For(0, height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                byte m = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = x + k;
                    if (q < 0 || q >= width) continue;
                    if (map[row + q] > m) m = map[row + q];
                }
                tmp[row + x] = m;
            }
        });

        Parallel.For(0, width, new ParallelOptions { CancellationToken = ct }, x =>
        {
            for (int y = 0; y < height; y++)
            {
                byte m = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = y + k;
                    if (q < 0 || q >= height) continue;
                    if (tmp[q * width + x] > m) m = tmp[q * width + x];
                }
                map[y * width + x] = m;
            }
        });
    }

    private static void BoxBlurGray(byte[] gray, int width, int height, int radius, CancellationToken ct)
    {
        var tmp = new byte[gray.Length];

        Parallel.For(0, height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int sum = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = x + k;
                    if (q < 0 || q >= width) continue;
                    sum += gray[row + q]; n++;
                }
                tmp[row + x] = (byte)(sum / n);
            }
        });

        Parallel.For(0, width, new ParallelOptions { CancellationToken = ct }, x =>
        {
            for (int y = 0; y < height; y++)
            {
                int sum = 0, n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = y + k;
                    if (q < 0 || q >= height) continue;
                    sum += tmp[q * width + x]; n++;
                }
                gray[y * width + x] = (byte)(sum / n);
            }
        });
    }
}
