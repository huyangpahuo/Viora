using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


public sealed class StainedGlassStage : StageBase
{
    public override string Name => "StainedGlass";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 20), 6, 64);
        int lead = Math.Clamp(Int(context.Parameters, "lead", 2), 1, 8);

        // jittered seed lattice
        int cols = (w + cell - 1) / cell + 1, rows = (h + cell - 1) / cell + 1;
        var seeds = new (double X, double Y, int B, int G, int R)[cols, rows];
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                double sx = gx * cell + (ImageOps.Hash(gx, gy, 31) - 0.5) * cell * 0.9;
                double sy = gy * cell + (ImageOps.Hash(gx, gy, 37) - 0.5) * cell * 0.9;
                int px_ = Math.Clamp((int)sx, 0, w - 1), py_ = Math.Clamp((int)sy, 0, h - 1);
                int i = py_ * stride + px_ * 4;
                seeds[gx, gy] = (sx, sy, px[i], px[i + 1], px[i + 2]);
            }

        var owner = new int[w * h];

        // nearest seed per pixel (search the 3×3 neighboring lattice cells)
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x / cell, gy = y / cell;
                double best = double.MaxValue; int bestId = 0;
                for (int dy = -1; dy <= 1; dy++)
                    for (int dx = -1; dx <= 1; dx++)
                    {
                        int cx = gx + dx, cy = gy + dy;
                        if (cx < 0 || cy < 0 || cx >= cols || cy >= rows) continue;
                        var s = seeds[cx, cy];
                        double d = (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y);
                        int id = cy * cols + cx;
                        if (d < best || (d == best && id < bestId)) { best = d; bestId = id; }
                    }
                owner[y * w + x] = bestId;
            }
        });

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                bool isLead = false;
                if (x > 0 && owner[y * w + x - 1] != owner[y * w + x]) isLead = true;
                else if (y > 0 && owner[(y - 1) * w + x] != owner[y * w + x]) isLead = true;

                if (isLead)
                {
                    px[i] = 38; px[i + 1] = 36; px[i + 2] = 34;
                    continue;
                }

                int id = owner[y * w + x];
                var s = seeds[id % cols, id / cols];

                // translucent glass: brighten + saturate toward seed color
                double lift = 1.25;
                px[i] = ImageOps.Clamp((int)(s.B * lift));
                px[i + 1] = ImageOps.Clamp((int)(s.G * lift));
                px[i + 2] = ImageOps.Clamp((int)(s.R * lift));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

