using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class RisographStage : StageBase
{
    public override string Name => "Risograph";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int off = Int(context.Parameters, "offset", 2);
        double grain = Dbl(context.Parameters, "grain", 0.5);

        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        const byte paperB = 243, paperG = 240, paperR = 226;
        const byte inkAB = 122, inkAG = 44, inkAR = 226;   // riso blue-ish
        const byte inkBB = 84, inkBG = 58, inkBR = 240;    // riso red-pink

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = 1.0 - blurred[y * w + x] / 255.0;
                double n = (ImageOps.Hash(x, y, 7) - 0.5) * grain * 0.35;

                int xa = Math.Clamp(x + off, 0, w - 1);          // blue plate shifted
                int xb = Math.Clamp(x - off, 0, w - 1);          // red plate shifted
                double ta = 1.0 - blurred[y * w + xa] / 255.0;
                double tb = 1.0 - blurred[y * w + xb] / 255.0;

                double a = ta > 0.52 ? 1.0 : 0.0;
                double b = tb > 0.72 ? 1.0 : 0.0;

                double r = paperR, g = paperG, bl = paperB;
                if (a > 0) { r = inkAR; g = inkAG; bl = inkAB; }
                if (b > 0) { r = inkBR; g = inkBG; bl = inkBB; }
                if (a > 0 && b > 0) { r = 210; g = 30; bl = 140; } // overprint violet

                double f = 1.0 + n;
                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp((int)(bl * f));
                px[i + 1] = ImageOps.Clamp((int)(g * f));
                px[i + 2] = ImageOps.Clamp((int)(r * f));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

