using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class IsometricDioramaStage : StageBase
{
    public override string Name => "IsometricDiorama";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double scale = Dbl(context.Parameters, "scale", 0.62);
        double shadow = Dbl(context.Parameters, "shadow", 0.5);

        var original = (byte[])px.Clone();
        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        double cx = w / 2.0, cy = h / 2.0;
        // isometric diamond: X = cx + (sx-sy)*halfW, Y = cy + (sx+sy)*halfH, sx/sy ∈ [-1,1]
        double halfW = w * 0.43 * scale;
        double halfH = h * 0.40 * scale;
        int thickness = (int)(Math.Min(w, h) * 0.035 * scale) + 3;

        // desk background: soft radial tone
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                double d = Math.Abs(x - cx) / cx + Math.Abs(y - cy) / cy;
                byte v = (byte)(46 + Math.Clamp(1.35 - d, 0, 1) * 26);
                outPx[i] = (byte)(v - 8); outPx[i + 1] = v; outPx[i + 2] = (byte)(v + 6);
            }

        var quad = new byte[4];
        int shOff = (int)(h * 0.03) + 2;

        // main pass: image diamond, extruded base walls, shadow
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;

                // shadow (behind everything, slightly larger and shifted down)
                {
                    double dxs = (x - cx) / (halfW * 1.05);
                    double dys = (y - cy - shOff) / (halfH * 1.05);
                    double ssx = (dxs + dys) / 2, ssy = (dys - dxs) / 2;
                    if (Math.Abs(ssx) <= 1 && Math.Abs(ssy) <= 1)
                    {
                        double k = shadow * 0.5 * (1.1 - (Math.Abs(ssx) + Math.Abs(ssy)) / 2);
                        outPx[i] = ImageOps.Clamp((int)(outPx[i] * (1 - k) + 10 * k));
                        outPx[i + 1] = ImageOps.Clamp((int)(outPx[i + 1] * (1 - k) + 12 * k));
                        outPx[i + 2] = ImageOps.Clamp((int)(outPx[i + 2] * (1 - k) + 10 * k));
                    }
                }

                double dx = (x - cx) / halfW, dy = (y - cy) / halfH;
                double sx = (dx + dy) / 2, sy = (dy - dx) / 2;

                if (Math.Abs(sx) <= 1 && Math.Abs(sy) <= 1)
                {
                    // the picture itself, mapped onto the diamond floor
                    ImageOps.SampleBilinear(original, stride, w, h,
                        (sx * 0.5 + 0.5) * (w - 1), (sy * 0.5 + 0.5) * (h - 1), quad);
                    outPx[i] = quad[0]; outPx[i + 1] = quad[1]; outPx[i + 2] = quad[2];
                    continue;
                }

                // extruded base walls: pixels just below the diamond's lower edges
                double dy2 = (y - thickness - cy) / halfH;
                double sx2 = (dx + dy2) / 2, sy2 = (dy2 - dx) / 2;
                bool onRightWall = sx2 >= 1 && sx2 <= 1.15 && Math.Abs(sy2) <= 1;
                bool onLeftWall = sy2 >= 1 && sy2 <= 1.15 && Math.Abs(sx2) <= 1;
                if (onRightWall || onLeftWall)
                {
                    double uu = onRightWall ? 1 : sx2, vv = onLeftWall ? 1 : sy2;
                    double lu = (uu + vv) / 2, lv = (vv - uu) / 2;
                    if (lu >= -1 && lu <= 1 && lv >= -1 && lv <= 1)
                    {
                        ImageOps.SampleBilinear(original, stride, w, h,
                            (lu * 0.5 + 0.5) * (w - 1), (lv * 0.5 + 0.5) * (h - 1), quad);
                        outPx[i] = ImageOps.Clamp((int)(quad[0] * 0.42));
                        outPx[i + 1] = ImageOps.Clamp((int)(quad[1] * 0.42));
                        outPx[i + 2] = ImageOps.Clamp((int)(quad[2] * 0.42));
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.3 + 0.7 * (y + 1) / (double)h));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

