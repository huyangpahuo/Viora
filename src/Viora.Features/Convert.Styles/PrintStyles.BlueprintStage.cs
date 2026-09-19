using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class BlueprintStage : StageBase
{
    public override string Name => "Blueprint";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double detail = Dbl(context.Parameters, "detail", 0.5);
        double grid = Dbl(context.Parameters, "grid", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = (1f - (float)detail) * 200f + 60f;
        int gridStep = 40;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // drafting-paper blue with subtle vignette
                double vig = 1.0 - 0.18 * Math.Abs((x / (double)w - 0.5)) * 2 * Math.Abs((y / (double)h - 0.5)) * 2;
                int b = (int)(150 * vig), g = (int)(64 * vig), r = (int)(24 * vig);

                bool onGrid = (x % gridStep == 0) || (y % gridStep == 0);
                if (onGrid && grid > 0.01)
                {
                    b += (int)(40 * grid); g += (int)(28 * grid); r += (int)(18 * grid);
                }

                if (mag[y * w + x] > threshold)
                {
                    b = 235; g = 244; r = 255; // white ink lines
                }

                px[i] = (byte)Math.Min(255, b);
                px[i + 1] = (byte)Math.Min(255, g);
                px[i + 2] = (byte)Math.Min(255, r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

