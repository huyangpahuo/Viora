using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class AsciiArtStage : StageBase
{
    public override string Name => "AsciiArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 10), 6, 32);
        bool invert = Dbl(context.Parameters, "invert", 0.0) > 0.5;

        var luma = ImageOps.LumaMap(src);

        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;
        int cols = (w + cell - 1) / cell, rows = (h + cell - 1) / cell;

        // terminal background
        for (int i = 0; i < outPx.Length; i += 4)
        {
            outPx[i] = 16; outPx[i + 1] = 17; outPx[i + 2] = 15; outPx[i + 3] = 255;
        }

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                long sum = 0; int n = 0;
                int x1 = Math.Min(w, (cx + 1) * cell), y1 = Math.Min(h, (cy + 1) * cell);
                for (int y = cy * cell; y < y1; y++)
                    for (int x = cx * cell; x < x1; x++)
                    {
                        sum += luma[y * w + x]; n++;
                    }

                double t = invert ? 1.0 - sum / (double)(n * 255) : sum / (double)(n * 255);
                int glyph = Math.Clamp((int)Math.Round(t * (ImageOps.AsciiChars.Length - 1)), 0, ImageOps.AsciiChars.Length - 1);
                if (glyph == 0) continue;

                // character color ramps with brightness (terminal green)
                byte b = (byte)(90 + t * 60), g = (byte)(140 + t * 110), r = (byte)(60 + t * 70);

                for (int gy = 0; gy < 7; gy++)
                {
                    int py = cy * cell + gy * cell / 7;
                    if (py >= h) break;
                    for (int gx = 0; gx < 5; gx++)
                    {
                        int pxc = cx * cell + gx * cell / 5;
                        if (pxc >= w) break;
                        if (!ImageOps.AsciiBit(glyph, gx, gy)) continue;

                        int i = py * outStride + pxc * 4;
                        outPx[i] = b; outPx[i + 1] = g; outPx[i + 2] = r;
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (cy + 1) / (double)rows));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

