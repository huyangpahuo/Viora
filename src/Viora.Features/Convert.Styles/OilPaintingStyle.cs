using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

/// <summary>Parameter keys for the Van-Gogh-style oil painting preset.</summary>
internal static class OilPaintParams
{
    public const string StrokeSize = "strokeSize";
    public const string PaintColors = "paintColors";
    public const string Texture = "texture";
}

/// <summary>
/// Oil painting preset: Kuwahara edge-preserving smoothing flattens regions into
/// brush-sized patches, a flow-guided relief pass smears them into directional
/// impasto strokes, and a final posterize keeps the palette bold and limited.
/// </summary>
public sealed class OilPaintingPreset : IStylePreset
{
    public const string PresetId = "builtin.oil-painting";

    public string Id => PresetId;

    public string DisplayNameKey => "Preset.OilPainting.Name";

    public string DescriptionKey => "Preset.OilPainting.Description";

    public string? IconGlyph => "\uE790"; // Segoe MDL2 Color

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter(OilPaintParams.StrokeSize, "Param.StrokeSize", 4, 2, 8, 1),
        new PresetParameter(OilPaintParams.PaintColors, "Param.PaintColors", 12, 4, 32, 1),
        new PresetParameter(OilPaintParams.Texture, "Param.Texture", 0.5, 0.0, 1.0, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[]
        {
            new KuwaharaStage(),
            new StrokeReliefStage(),
            new PosterizeStage(),
        };
}

/// <summary>
/// Kuwahara filter: for each pixel picks the flattest (lowest-luma-variance) of the four
/// corner quadrants and outputs its mean color — edge preserving, painterly patching.
/// Direct quadrant scan (no integral images) keeps memory flat at 4K export sizes.
/// </summary>
public sealed class KuwaharaStage : StageBase
{
    public override string Name => "Kuwahara";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int radius = Math.Clamp(Int(context.Parameters, OilPaintParams.StrokeSize, 4), 1, 12);
        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        var dst = new RgbaImageBuffer(width, height);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                long bestVar = long.MaxValue;
                byte bestB = 0, bestG = 0, bestR = 0;

                // Quadrants: (-,-) (-,+) (+,-) (+,+), each a radius-sized corner box.
                for (int q = 0; q < 4; q++)
                {
                    int x0 = (q & 1) == 0 ? x - radius : x;
                    int x1 = (q & 1) == 0 ? x : x + radius;
                    int y0 = (q & 2) == 0 ? y - radius : y;
                    int y1 = (q & 2) == 0 ? y : y + radius;
                    x0 = Math.Max(0, x0); y0 = Math.Max(0, y0);
                    x1 = Math.Min(width - 1, x1); y1 = Math.Min(height - 1, y1);

                    long sb = 0, sg = 0, sr = 0, sl = 0, sl2 = 0;
                    int n = 0;
                    for (int yy = y0; yy <= y1; yy++)
                    {
                        int row = yy * stride;
                        for (int xx = x0; xx <= x1; xx++)
                        {
                            int i = row + xx * 4;
                            int l = PixelOps.Luma(px, i);
                            sb += px[i]; sg += px[i + 1]; sr += px[i + 2];
                            sl += l; sl2 += (long)l * l;
                            n++;
                        }
                    }
                    if (n == 0) continue;

                    // variance = E[l²] − E[l]²  (×n to stay integral)
                    long variance = sl2 * n - sl * sl;
                    if (variance < bestVar)
                    {
                        bestVar = variance;
                        bestB = (byte)(sb / n); bestG = (byte)(sg / n); bestR = (byte)(sr / n);
                    }
                }

                int o = y * outStride + x * 4;
                outPx[o] = bestB; outPx[o + 1] = bestG; outPx[o + 2] = bestR;
                outPx[o + 3] = px[y * stride + x * 4 + 3];
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)height));
        });

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>
/// Stroke relief: reads the local iso-luma direction ("brush flow"), smears colors
/// along it for directional dabs, then adds impasto shading across the stroke and a
/// faint canvas weave. Strength scales with the texture parameter.
/// </summary>
public sealed class StrokeReliefStage : StageBase
{
    public override string Name => "StrokeRelief";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double texture = Dbl(context.Parameters, OilPaintParams.Texture, 0.5);
        int radius = Math.Clamp(Int(context.Parameters, OilPaintParams.StrokeSize, 4), 1, 12);
        if (texture <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        // 1) Luma + Sobel → double-angle flow field (cos2θ, sin2θ of the iso-luma line).
        var luma = new byte[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
                luma[y * width + x] = PixelOps.Luma(px, y * stride + x * 4);
        });

        var flowX = new float[width * height];
        var flowY = new float[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int gx = SobelX(luma, width, height, x, y);
                int gy = SobelY(luma, width, height, x, y);
                // θ of the gradient; the stroke runs along the perpendicular (iso-luma).
                double angle = Math.Atan2(gy, gx) + Math.PI / 2.0;
                flowY[y * width + x] = (float)Math.Cos(2 * angle);
                flowX[y * width + x] = (float)Math.Sin(2 * angle);
            }
        });

        // 2) Box-blur the field (double-angle avoids the 0/2π wrap) → coherent strokes.
        BoxBlurField(flowX, width, height, 3, cancellationToken);
        BoxBlurField(flowY, width, height, 3, cancellationToken);

        // 3) Apply: sample back along the flow, add impasto ridge shading + canvas weave.
        var dst = new RgbaImageBuffer(width, height);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;
        double strokeLen = radius * 0.9;
        double weaveGain = texture * 0.05;
        double ridgeGain = texture * 0.30;

        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                double fx = flowX[idx], fy = flowY[idx];
                double mag = Math.Sqrt(fx * fx + fy * fy);
                double dirX = 0, dirY = 0;
                if (mag > 1e-3)
                {
                    // halve the double-angle back to the stroke direction
                    double half = Math.Atan2(fy, fx) / 2.0;
                    dirX = Math.Cos(half);
                    dirY = Math.Sin(half);
                }

                int sx = Math.Clamp((int)Math.Round(x - dirX * strokeLen), 0, width - 1);
                int sy = Math.Clamp((int)Math.Round(y - dirY * strokeLen), 0, height - 1);

                int i = y * stride + x * 4;
                int j = sy * stride + sx * 4;

                // smear: 50/50 between the pixel and its upstream sample along the flow
                double b = 0.5 * (px[i] + px[j]);
                double g = 0.5 * (px[i + 1] + px[j + 1]);
                double r = 0.5 * (px[i + 2] + px[j + 2]);

                // impasto: brighten one side of the stroke, darken the other
                double nx = -dirY, ny = dirX;
                double ridge = Math.Sin((x * nx + y * ny) * (Math.PI / Math.Max(2.0, radius)));
                double shade = 1.0 + ridgeGain * ridge;

                // canvas weave: faint orthogonal thread pattern
                double weave = 1.0 + weaveGain * Math.Sin(y * (Math.PI / 2.4)) + weaveGain * Math.Sin(x * (Math.PI / 2.4));

                int o = y * outStride + x * 4;
                outPx[o] = PixelOps.Clamp((int)(b * shade * weave));
                outPx[o + 1] = PixelOps.Clamp((int)(g * shade * weave));
                outPx[o + 2] = PixelOps.Clamp((int)(r * shade * weave));
                outPx[o + 3] = px[i + 3];
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)height));
        });

        context.Working = dst;
        return Task.FromResult(context);
    }

    private static int SobelX(byte[] luma, int width, int height, int x, int y)
    {
        int xm = Math.Max(0, x - 1), xp = Math.Min(width - 1, x + 1);
        int ym = Math.Max(0, y - 1), yp = Math.Min(height - 1, y + 1);
        return
            -luma[ym * width + xm] - 2 * luma[y * width + xm] - luma[yp * width + xm]
            + luma[ym * width + xp] + 2 * luma[y * width + xp] + luma[yp * width + xp];
    }

    private static int SobelY(byte[] luma, int width, int height, int x, int y)
    {
        int xm = Math.Max(0, x - 1), xp = Math.Min(width - 1, x + 1);
        int ym = Math.Max(0, y - 1), yp = Math.Min(height - 1, y + 1);
        return
            -luma[ym * width + xm] - 2 * luma[ym * width + x] - luma[ym * width + xp]
            + luma[yp * width + xm] + 2 * luma[yp * width + x] + luma[yp * width + xp];
    }

    /// <summary>In-place separable box blur on a float field (two passes).</summary>
    private static void BoxBlurField(float[] field, int width, int height, int radius, CancellationToken ct)
    {
        var tmp = new float[field.Length];

        Parallel.For(0, height, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                double sum = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = x + k;
                    if (q < 0 || q >= width) continue;
                    sum += field[row + q]; n++;
                }
                tmp[row + x] = (float)(sum / n);
            }
        });

        Parallel.For(0, width, new ParallelOptions { CancellationToken = ct }, x =>
        {
            for (int y = 0; y < height; y++)
            {
                double sum = 0; int n = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = y + k;
                    if (q < 0 || q >= height) continue;
                    sum += tmp[q * width + x]; n++;
                }
                field[y * width + x] = (float)(sum / n);
            }
        });
    }
}

/// <summary>Posterize: quantizes each channel to N levels — keeps the palette bold.</summary>
public sealed class PosterizeStage : StageBase
{
    public override string Name => "Posterize";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int levels = Math.Clamp(Int(context.Parameters, OilPaintParams.PaintColors, 12), 2, 64);
        var src = context.Working!;
        var px = src.Pixels;
        int stride = src.Stride;
        double step = 255.0 / (levels - 1);

        var lut = new byte[256];
        for (int v = 0; v < 256; v++) lut[v] = (byte)(Math.Round(v / step) * step);

        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            int row = y * stride;
            for (int x = 0; x < src.Width; x++)
            {
                int i = row + x * 4;
                px[i] = lut[px[i]];
                px[i + 1] = lut[px[i + 1]];
                px[i + 2] = lut[px[i + 2]];
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
