using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class NewspaperStage : StageBase
{
    public override string Name => "Newspaper";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.6);
        double grain = Dbl(context.Parameters, "grain", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double v = lut[luma[y * w + x]] / 255.0;
                // fine print-dot texture
                double dot = ((x + y) % 3 == 0) ? 0.94 : 1.0;
                double n = 1.0 + (ImageOps.Hash(x, y, 11) - 0.5) * grain * 0.22;
                double ink = 1.0 - v * dot * n;

                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp((int)(228 - ink * 205));
                px[i + 1] = ImageOps.Clamp((int)(225 - ink * 202));
                px[i + 2] = ImageOps.Clamp((int)(214 - ink * 190));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

