using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class ComicStage : StageBase
{
    public override string Name => "Comic";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;

        int colors = Math.Clamp(Int(context.Parameters, "colors", 5), 3, 8);
        int thickness = Math.Clamp(Int(context.Parameters, "thickness", 2), 1, 4);
        double detail = Dbl(context.Parameters, "detail", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);

        var poster = ImageOps.PosterizeLut(colors);
        float threshold = (1f - (float)detail) * 160f + 40f;

        // mild blur pass for clean flats
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                    px[i + c] = poster[px[i + c]];

                // thick ink on strong edges (dilated via neighborhood max)
                bool ink = false;
                for (int k = 0; k < thickness && !ink; k++)
                    for (int dy = -k; dy <= k && !ink; dy++)
                        for (int dx = -k; dx <= k && !ink; dx++)
                        {
                            int nx = Math.Clamp(x + dx, 0, w - 1), ny = Math.Clamp(y + dy, 0, h - 1);
                            if (mag[ny * w + nx] > threshold) ink = true;
                        }
                if (ink)
                {
                    px[i] = 30; px[i + 1] = 26; px[i + 2] = 34;
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

