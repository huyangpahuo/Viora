using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Edge re-inking (style rule 5 / §2.4): the reference has almost no stroked outlines —
/// separation comes from color contrast. Optional subtle dark-tinted ink on boundaries
/// between colors with a large luma delta only, so boundaries "read" cleanly after
/// consolidation. Default is deliberately low.
/// </summary>
public sealed class EdgeInkStage : StageBase
{
    public override string Name => "EdgeInk";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double strength = Dbl(context.Parameters, ParamKeys.Edges, 0.25); // 0..1
        if (strength <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        // Sobel on luma; ink where gradient is high (region boundary, not interior texture).
        var luma = new byte[width * height];
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
                luma[y * width + x] = PixelOps.Luma(px, y * stride + x * 4);
        });

        int threshold = (int)(200 - strength * 140); // stronger → lower threshold → more ink
        byte inkLevel = (byte)(40 + 30 * (1 - strength));

        Parallel.For(1, height - 1, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 1; x < width - 1; x++)
            {
                int gx =
                    -luma[(y - 1) * width + x - 1] - 2 * luma[y * width + x - 1] - luma[(y + 1) * width + x - 1]
                    + luma[(y - 1) * width + x + 1] + 2 * luma[y * width + x + 1] + luma[(y + 1) * width + x + 1];
                int gy =
                    -luma[(y - 1) * width + x - 1] - 2 * luma[(y - 1) * width + x] - luma[(y - 1) * width + x + 1]
                    + luma[(y + 1) * width + x - 1] + 2 * luma[(y + 1) * width + x] + luma[(y + 1) * width + x + 1];

                if (gx * gx + gy * gy < threshold * threshold) continue;

                int i = y * stride + x * 4;
                px[i] = PixelOps.Clamp((px[i] * 2 + inkLevel) / 3);
                px[i + 1] = PixelOps.Clamp((px[i + 1] * 2 + inkLevel) / 3);
                px[i + 2] = PixelOps.Clamp((px[i + 2] * 2 + inkLevel) / 3);
            }
        });

        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
