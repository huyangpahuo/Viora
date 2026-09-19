using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class CollageStage : StageBase
{
    public override string Name => "Collage";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int tiles = Math.Clamp(Int(context.Parameters, "tiles", 4), 2, 8);
        double rotation = Dbl(context.Parameters, "rotation", 0.5);

        var original = (byte[])px.Clone();
        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        // Paper background with light grain — visible in the gaps between scraps.
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                byte n = (byte)(242 + ImageOps.Hash(x, y, 3) * 10);
                outPx[i] = n; outPx[i + 1] = n; outPx[i + 2] = (byte)(n + 3);
            }

        double tileW = w / (double)tiles, tileH = h / (double)tiles;
        double maxAngle = (2.0 + rotation * 5.0) * Math.PI / 180.0;

        // Draw the scraps row by row; each one is rotated, shifted and torn.
        for (int ty = 0; ty < tiles; ty++)
        {
            for (int tx = 0; tx < tiles; tx++)
            {
                double angle = (ImageOps.Hash(tx, ty, 13) - 0.5) * 2 * maxAngle;
                double cos = Math.Cos(angle), sin = Math.Sin(angle);

                // scrap center drifts from the ideal grid cell
                double cxt = (tx + 0.5) * tileW + (ImageOps.Hash(tx, ty, 15) - 0.5) * tileW * 0.16;
                double cyt = (ty + 0.5) * tileH + (ImageOps.Hash(tx, ty, 17) - 0.5) * tileH * 0.16;

                // the scrap shows a DIFFERENT part of the picture than its grid slot
                double srcShiftX = (ImageOps.Hash(tx, ty, 19) - 0.5) * tileW * 0.35;
                double srcShiftY = (ImageOps.Hash(tx, ty, 23) - 0.5) * tileH * 0.35;

                double halfW = tileW * 0.46, halfH = tileH * 0.46;
                int x0 = Math.Max(0, (int)(cxt - tileW * 0.75));
                int x1 = Math.Min(w - 1, (int)(cxt + tileW * 0.75));
                int y0 = Math.Max(0, (int)(cyt - tileH * 0.75));
                int y1 = Math.Min(h - 1, (int)(cyt + tileH * 0.75));

                for (int y = y0; y <= y1; y++)
                {
                    for (int x = x0; x <= x1; x++)
                    {
                        double rx = x + 0.5 - cxt, ry = y + 0.5 - cyt;
                        // inverse-rotate the output point into the scrap's frame
                        double lx = cos * rx + sin * ry;
                        double ly = -sin * rx + cos * ry;
                        if (Math.Abs(lx) > halfW || Math.Abs(ly) > halfH) continue;

                        // torn edge: irregular white bite around the border
                        double edgeDist = halfW - Math.Abs(lx) < halfH - Math.Abs(ly)
                            ? halfW - Math.Abs(lx) : halfH - Math.Abs(ly);
                        double tear = 1.5 + ImageOps.Hash((int)(lx + 500), (int)(ly + 500), 25) * 3.5;
                        if (edgeDist < tear) continue;

                        // sample a shifted source region so neighbors don't repeat
                        double sx = (tx + 0.5) * tileW + lx + srcShiftX;
                        double sy = (ty + 0.5) * tileH + ly + srcShiftY;
                        if (sx < 0 || sy < 0 || sx >= w - 1 || sy >= h - 1) continue;

                        var quad = new byte[4];
                        ImageOps.SampleBilinear(original, stride, w, h, sx, sy, quad);

                        int o = y * outStride + x * 4;
                        outPx[o] = quad[0]; outPx[o + 1] = quad[1]; outPx[o + 2] = quad[2];

                        // drop-shadow line along each scrap's bottom-right inside edge
                        if (edgeDist < tear + 2.5)
                        {
                            outPx[o] = ImageOps.Clamp(outPx[o] - 26);
                            outPx[o + 1] = ImageOps.Clamp(outPx[o + 1] - 26);
                            outPx[o + 2] = ImageOps.Clamp(outPx[o + 2] - 26);
                        }
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (ty + 1) / (double)tiles));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

