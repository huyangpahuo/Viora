using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class HalftoneStage : StageBase
{
    public override string Name => "Halftone";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 8), 3, 32);
        double contrast = Dbl(context.Parameters, "contrast", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, Math.Max(1, cell / 3));

        // Paper keeps a lightened trace of the original color; ink dots carry the tone.
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp(px[i] * 35 / 100 + 220);
                px[i + 1] = ImageOps.Clamp(px[i + 1] * 35 / 100 + 220);
                px[i + 2] = ImageOps.Clamp(px[i + 2] * 35 / 100 + 218);
            }
        });

        // Staggered dot screen (odd rows shifted half a cell) sized by cell-average luma.
        int rows = (h + cell - 1) / cell;
        for (int cy = 0; cy < rows; cy++)
        {
            double stagger = (cy & 1) == 1 ? cell * 0.5 : 0.0;
            int cols = (w + cell - 1) / cell + 1;
            for (int cx = 0; cx < cols; cx++)
            {
                int ccx = (int)(cx * cell + cell / 2.0 + stagger - cell * 0.5);
                int ccy = cy * cell + cell / 2;
                int sx = Math.Clamp(ccx, 0, w - 1), sy = Math.Clamp(ccy, 0, h - 1);

                // average the luma over the cell footprint
                long sum = 0; int n = 0;
                int x1 = Math.Min(w, ccx + cell), y1 = Math.Min(h, ccy + cell);
                for (int yy = Math.Max(0, ccy); yy < y1; yy++)
                    for (int xx = Math.Max(0, ccx); xx < x1; xx++)
                    {
                        sum += lut[blurred[yy * w + xx]]; n++;
                    }
                double t = n > 0 ? sum / (double)(n * 255) : 1.0;

                double radius = cell * 0.68 * Math.Sqrt(1.0 - t);
                if (radius < 0.4) continue;
                ImageOps.FillCircle(px, stride, w, h, ccx + 0.5, ccy + 0.5, radius, 24, 22, 26);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (cy + 1) / (double)rows));
        }

        return Task.FromResult(context);
    }
}

