using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class ClayStage : StageBase
{
    public override string Name => "Clay";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double smoothing = Dbl(context.Parameters, "smoothing", 0.7);
        double relief = Dbl(context.Parameters, "relief", 0.5);

        // Heavy color smoothing gives the flattened, thumb-pressed surface;
        // the original still bleeds through so hues survive.
        var flat = ImageOps.BoxBlurColor(px, stride, w, h, 3 + (int)(smoothing * 9));

        var luma = ImageOps.LumaMap(src);
        var soft = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(soft, w, h, 2 + (int)(smoothing * 5));

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int f = y * stride + x * 4;

                    // clay = mostly flattened color, a little original detail
                    double mix = 0.62;
                    int b = (int)(flat[f] * mix + px[i] * (1 - mix));
                    int g = (int)(flat[f + 1] * mix + px[i + 1] * (1 - mix));
                    int r = (int)(flat[f + 2] * mix + px[i + 2] * (1 - mix));

                    // matte pastel lift: pull toward milky tone
                    b = b + (150 - b) * 14 / 100;
                    g = g + (148 - g) * 14 / 100;
                    r = r + (155 - r) * 14 / 100;

                    // rounded-volume shading from the blurred vertical slope
                    double slope = (soft[Math.Min(h - 1, y + 1) * w + x] - soft[Math.Max(0, y - 1) * w + x]) / 255.0;
                    double shade = 1.0 + slope * relief * 2.0;

                    px[i] = ImageOps.Clamp((int)(b * shade));
                    px[i + 1] = ImageOps.Clamp((int)(g * shade));
                    px[i + 2] = ImageOps.Clamp((int)(r * shade));
                }
                progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
            }
        }, ct);

        return context;
    }
}

