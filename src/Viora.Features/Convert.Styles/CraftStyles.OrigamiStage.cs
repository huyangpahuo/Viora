using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class OrigamiStage : StageBase
{
    public override string Name => "Origami";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 28), 8, 96);
        double crease = Dbl(context.Parameters, "crease", 0.5);

        // Folded-paper facets read from a smoothed copy of the picture.
        var smooth = ImageOps.BoxBlurColor(src.Pixels, stride, w, h, Math.Clamp(cell / 4, 1, 8));

        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;
        int cols = (w + cell - 1) / cell + 1, rows = (h + cell - 1) / cell + 1;
        var points = new (double X, double Y)[cols, rows];
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                double jx = (ImageOps.Hash(gx, gy, 41) - 0.5) * cell * 0.5;
                double jy = (ImageOps.Hash(gx, gy, 43) - 0.5) * cell * 0.5;
                points[gx, gy] = (gx * (double)cell - jx, gy * (double)cell - jy);
            }

        for (int gy = 0; gy < rows - 1; gy++)
        {
            for (int gx = 0; gx < cols - 1; gx++)
            {
                var p00 = points[gx, gy]; var p10 = points[gx + 1, gy];
                var p01 = points[gx, gy + 1]; var p11 = points[gx + 1, gy + 1];
                var diag = (X: (p00.X + p11.X) / 2, Y: (p00.Y + p11.Y) / 2);

                var tris = new ((double X, double Y) A, (double X, double Y) B, (double X, double Y) C)[]
                {
                    (p00, p10, diag), (p00, diag, p01), (p10, p11, diag), (p01, diag, p11),
                };

                int triId = gx * 4 + gy * 97;
                foreach (var (a, b, c) in tris)
                {
                    double cx = (a.X + b.X + c.X) / 3, cy = (a.Y + b.Y + c.Y) / 3;
                    int sx = Math.Clamp((int)cx, 0, w - 1), sy = Math.Clamp((int)cy, 0, h - 1);
                    int pi = sy * stride + sx * 4;

                    // fold shading: each facet tilts toward/away from the light
                    double fold = 0.86 + ImageOps.Hash(gx * 2 + (triId++ & 1), gy, 67) * (0.10 + crease * 0.22);

                    int bB = ImageOps.Clamp((int)((smooth[pi] * 0.45 + 240 * 0.55) * fold));
                    int bG = ImageOps.Clamp((int)((smooth[pi + 1] * 0.45 + 236 * 0.55) * fold));
                    int bR = ImageOps.Clamp((int)((smooth[pi + 2] * 0.45 + 230 * 0.55) * fold));
                    ImageOps.FillTriangle(outPx, outStride, w, h, a, b, c, bB, bG, bR, shadeEdges: false);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (gy + 1) / (double)(rows - 1)));
        }

        // paper grain over the facets
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                double n = (ImageOps.Hash(x, y, 47) - 0.5) * (6 + 14 * crease);
                byte add = ImageOps.Clamp((int)(n));
                outPx[i] = ImageOps.Clamp(outPx[i] + add);
                outPx[i + 1] = ImageOps.Clamp(outPx[i + 1] + add);
                outPx[i + 2] = ImageOps.Clamp(outPx[i + 2] + add);
            }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

