using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class PaperCutStage : StageBase
{
    public override string Name => "PaperCut";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int layers = Math.Clamp(Int(context.Parameters, "layers", 5), 2, 8);
        int off = Math.Clamp(Int(context.Parameters, "offset", 2), 1, 8);

        var poster = ImageOps.PosterizeLut(layers);
        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                    px[i + c] = ImageOps.Clamp((int)(poster[px[i + c]] * 1.08)); // bright paper tones

                // shadow: this pixel sits on a higher layer than the one behind (offset sample)
                int sx = Math.Max(0, x - off), sy = Math.Max(0, y - off);
                if (luma[y * w + x] > luma[sy * w + sx] + 8)
                {
                    px[i] = ImageOps.Clamp(px[i] - 70);
                    px[i + 1] = ImageOps.Clamp(px[i + 1] - 64);
                    px[i + 2] = ImageOps.Clamp(px[i + 2] - 56);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

