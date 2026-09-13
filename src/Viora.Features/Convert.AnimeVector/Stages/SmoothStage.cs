using System.Threading.Tasks;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Edge-preserving smoothing: iterated separable bilateral-style filter with a small
/// radius and luma-range weighting. Flattens texture (style rule 2: zero gradients/texture)
/// while keeping region boundaries crisp for the quantizer.
/// </summary>
public sealed class SmoothStage : StageBase
{
    public override string Name => "Smooth";

    public override async Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double strength = Dbl(context.Parameters, ParamKeys.Smoothing, 0.6); // 0..1
        if (strength <= 0.01) return context;

        var src = context.Working!;
        int iterations = 1 + (int)Math.Round(strength * 5); // 1..6 passes
        int radius = 2 + (int)Math.Round(strength * 3);     // 2..5 px
        double sigmaRange = 24 + (1 - strength) * 48;        // stronger = keep fewer gradients

        var a = src.Pixels;
        var b = new byte[a.Length];
        int width = src.Width, height = src.Height, stride = src.Stride;

        for (int iter = 0; iter < iterations; iter++)
        {
            // Horizontal pass into b
            await Task.Run(() => FilterPass(a, b, width, height, stride, radius, sigmaRange, horizontal: true, iter, 2 * iterations, progress, cancellationToken), cancellationToken);
            // Vertical pass back into a
            await Task.Run(() => FilterPass(b, a, width, height, stride, radius, sigmaRange, horizontal: false, iter + 1, 2 * iterations, progress, cancellationToken), cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
        }

        return context;
    }

    private static void FilterPass(byte[] input, byte[] output, int width, int height, int stride,
        int radius, double sigmaRange, bool horizontal, int iter, int totalIters,
        IProgress<StageProgress>? progress, CancellationToken ct)
    {
        // Precompute range weights for luma deltas (0..255).
        var rangeWeights = new double[256];
        double inv2sr2 = 1.0 / (2.0 * sigmaRange * sigmaRange);
        for (int d = 0; d < 256; d++) rangeWeights[d] = Math.Exp(-d * d * inv2sr2);

        Parallel.For(0, horizontal ? height : width, new ParallelOptions { CancellationToken = ct }, line =>
        {
            for (int p = 0; p < (horizontal ? width : height); p++)
            {
                int i = horizontal ? line * stride + p * 4 : p * stride + line * 4;
                int centerLuma = Luma(input, i);

                double sumB = 0, sumG = 0, sumR = 0, sumW = 0;
                for (int k = -radius; k <= radius; k++)
                {
                    int q = p + k;
                    if (q < 0 || q >= (horizontal ? width : height)) continue;
                    int j = horizontal ? line * stride + q * 4 : q * stride + line * 4;
                    int luma = Luma(input, j);
                    double w = rangeWeights[Math.Abs(luma - centerLuma)];
                    sumB += input[j] * w; sumG += input[j + 1] * w; sumR += input[j + 2] * w;
                    sumW += w;
                }

                output[i] = ClampByte(sumB / sumW);
                output[i + 1] = ClampByte(sumG / sumW);
                output[i + 2] = ClampByte(sumR / sumW);
                output[i + 3] = input[i + 3];
            }
        });

        progress?.Report(new StageProgress("Smooth", 0, 1, (iter + 1) / (double)totalIters));
    }

    private static int Luma(byte[] px, int i) => (px[i + 2] * 299 + px[i + 1] * 587 + px[i] * 114) / 1000;

    private static byte ClampByte(double v) => v < 0 ? (byte)0 : v > 255 ? (byte)255 : (byte)v;
}
