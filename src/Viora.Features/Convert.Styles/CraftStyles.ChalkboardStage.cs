using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class ChalkboardStage : StageBase
{
    public override string Name => "Chalkboard";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double detail = Dbl(context.Parameters, "detail", 0.5);
        double dust = Dbl(context.Parameters, "dust", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = (1f - (float)detail) * 180f + 50f;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // slate board with smudge noise
                int b = 34, g = 48, r = 38;
                double smudge = ImageOps.Hash(x / 6, y / 6, 51);
                if (smudge > 0.8)
                {
                    b += 8; g += 10; r += 8;
                }

                // bright edges become chalk strokes with per-pixel chalk gaps
                double t = mag[y * w + x] / threshold;
                if (t > 1.0)
                {
                    double coverage = Math.Min(1.0, (t - 1.0) * 1.2) *
                                      (0.55 + 0.45 * ImageOps.Hash(x, y, 53));
                    double d = dust * ImageOps.Hash(x + 11, y + 7, 59) * 0.5;
                    double k = Math.Min(1.0, coverage + d * 0.3);
                    b = (int)(b + (225 - b) * k);
                    g = (int)(g + (232 - g) * k);
                    r = (int)(r + (228 - r) * k);
                }

                px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

