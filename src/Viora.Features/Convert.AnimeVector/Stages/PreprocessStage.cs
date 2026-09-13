using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Preprocess: mild saturation boost + contrast normalization toward the reference look
/// (§2.2: uniformly high saturation 0.6–1.0). Pure per-pixel, fully parallel.
/// </summary>
public sealed class PreprocessStage : StageBase
{
    public override string Name => "Preprocess";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double saturation = Dbl(context.Parameters, ParamKeys.Saturation, 1.15);
        var src = context.Working!;
        var px = src.Pixels;
        int stride = src.Stride;

        // Saturation around luma (1.0 = unchanged).
        double s = saturation;
        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            int row = y * stride;
            for (int x = 0; x < src.Width; x++)
            {
                int i = row + x * 4;
                int luma = (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;
                px[i] = PixelOps.Clamp((int)(luma + (px[i] - luma) * s));
                px[i + 1] = PixelOps.Clamp((int)(luma + (px[i + 1] - luma) * s));
                px[i + 2] = PixelOps.Clamp((int)(luma + (px[i + 2] - luma) * s));
            }
            progress?.Report(new StageProgress(Name, 0, 1, y / (double)src.Height));
        });

        return Task.FromResult(context);
    }
}
