using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class WoodcutStage : StageBase
{
    public override string Name => "Woodcut";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.7);
        double density = Dbl(context.Parameters, "density", 0.6);

        var contrastLut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 2);

        const byte paperB = 236, paperG = 230, paperR = 214;
        const byte ink = 26;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                double t = 1.0 - blurred[y * w + x] / 255.0; // darkness
                double period = 3.0 + (1.0 - t) * 14.0 / Math.Max(0.15, density);
                double linePos = (y + 3.0 * Math.Sin(x * 0.06)) % period;
                linePos = linePos < 0 ? linePos + period : linePos;
                double cut = period * t * 0.75;
                double v = linePos < cut ? 1.0 : 0.0;

                byte l = contrastLut[luma[y * w + x]];
                double k = v * 0.9 + (255 - l) / 255.0 * 0.1; // keep some tone in highlights
                px[i] = ImageOps.Clamp((int)(paperB + (ink - paperB) * k));
                px[i + 1] = ImageOps.Clamp((int)(paperG + (ink - paperG) * k));
                px[i + 2] = ImageOps.Clamp((int)(paperR + (ink - paperR) * k));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

