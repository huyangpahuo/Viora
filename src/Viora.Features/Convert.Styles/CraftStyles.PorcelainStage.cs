using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class PorcelainStage : StageBase
{
    public override string Name => "Porcelain";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double gloss = Dbl(context.Parameters, "gloss", 0.5);
        double smoothing = Dbl(context.Parameters, "smoothing", 0.7);

        int radius = 2 + (int)(smoothing * 6);

        // Actually-blurred color copy (the glaze base) + soft luma for the sheen mask.
        var glaze = ImageOps.BoxBlurColor(px, stride, w, h, radius);
        var luma = ImageOps.LumaMap(src);
        var soft = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(soft, w, h, Math.Max(1, radius / 2));

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                int f = y * stride + x * 4;
                double t = soft[y * w + x] / 255.0;

                // cool glazed white carries most of the tone; image keeps a third
                double b = glaze[f] * 0.34 + 238 * 0.66;
                double g = glaze[f + 1] * 0.34 + 243 * 0.66;
                double r = glaze[f + 2] * 0.34 + 246 * 0.66;

                // specular glaze: bright zones get a hard highlight, curves get sheen
                double spec = Math.Pow(Math.Max(0, t - 0.52) / 0.48, 1.6) * gloss;
                double rim = Math.Pow(1 - t, 2.2) * 0.10 * gloss;
                r += 235 * spec + 235 * rim;
                g += 238 * spec + 238 * rim;
                b += 242 * spec + 242 * rim;

                px[i] = ImageOps.Clamp((int)b);
                px[i + 1] = ImageOps.Clamp((int)g);
                px[i + 2] = ImageOps.Clamp((int)r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        }

        return Task.FromResult(context);
    }
}

