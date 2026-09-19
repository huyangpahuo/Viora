using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class LowPolyStage : StageBase
{
    public override string Name => "LowPoly";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 24), 6, 96);
        double jitter = Dbl(context.Parameters, "jitter", 0.6);

        // Facet colors come from a blurred copy — single-pixel sampling makes facets noisy.
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
                double jx = (ImageOps.Hash(gx, gy, 1) - 0.5) * cell * jitter;
                double jy = (ImageOps.Hash(gx, gy, 2) - 0.5) * cell * jitter;
                points[gx, gy] = (gx * (double)cell - jx, gy * (double)cell - jy);
            }

        await Task.Run(() =>
        {
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

                    foreach (var (a, b, c) in tris)
                    {
                        double cx = (a.X + b.X + c.X) / 3, cy = (a.Y + b.Y + c.Y) / 3;
                        int sx = Math.Clamp((int)cx, 0, w - 1), sy = Math.Clamp((int)cy, 0, h - 1);
                        int i = sy * stride + sx * 4;
                        ImageOps.FillTriangle(outPx, outStride, w, h, a, b, c, smooth[i], smooth[i + 1], smooth[i + 2], shadeEdges: false);
                    }
                }
                progress?.Report(new StageProgress(Name, 0, 1, (gy + 1) / (double)(rows - 1)));
            }
        }, ct);

        context.Working = dst;
        return context;
    }
}

