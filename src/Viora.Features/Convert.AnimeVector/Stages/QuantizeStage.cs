using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Color quantization: k-means over RGB seeded by histogram peaks (deterministic),
/// constrained to k = palette-size. Implements style rule 1 (bounded palette collapse).
/// Writes palette + index map into context.Properties for downstream stages.
/// </summary>
public sealed class QuantizeStage : StageBase
{
    public override string Name => "Quantize";

    public const string PaletteProperty = "palette";      // (byte B, byte G, byte R)[]
    public const string IndexMapProperty = "indexMap";    // int per pixel

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int k = Math.Clamp(Int(context.Parameters, ParamKeys.Colors, 8), 2, 32);
        var src = context.Working!;
        var px = src.Pixels;
        int count = src.Width * src.Height;
        int stride = src.Stride;

        // Build a 4-bit-per-channel histogram to seed centers on real color masses.
        var histogram = new Dictionary<int, (int Count, int B, int G, int R)>(2048);
        for (int y = 0; y < src.Height; y++)
        {
            for (int x = 0; x < src.Width; x++)
            {
                int i = stride * y + x * 4;
                int key = (px[i] >> 4 << 8) | (px[i + 1] >> 4 << 4) | (px[i + 2] >> 4);
                histogram[key] = histogram.TryGetValue(key, out var e)
                    ? (e.Count + 1, e.B + px[i], e.G + px[i + 1], e.R + px[i + 2])
                    : (1, px[i], px[i + 1], px[i + 2]);
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.15 * y / src.Height));
            cancellationToken.ThrowIfCancellationRequested();
        }

        // Seed: top-k histogram cells, k-means++ style spread via min-distance reselection.
        var centers = SeedCenters(histogram, k);
        var assignment = new int[count];

        // Lloyd iterations (fixed count for determinism).
        const int MaxIterations = 10;
        for (int iter = 0; iter < MaxIterations; iter++)
        {
            // Assign
            Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int i = stride * y + x * 4;
                    int best = 0, bestDist = int.MaxValue;
                    for (int c = 0; c < centers.Count; c++)
                    {
                        int db = px[i] - centers[c].B, dg = px[i + 1] - centers[c].G, dr = px[i + 2] - centers[c].R;
                        int d = db * db + dg * dg + dr * dr;
                        if (d < bestDist) { bestDist = d; best = c; }
                    }
                    assignment[y * src.Width + x] = best;
                }
            });
            progress?.Report(new StageProgress(Name, 0, 1, 0.2 + 0.6 * iter / MaxIterations));

            // Update
            var sums = new long[centers.Count * 4];
            Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
            {
                for (int x = 0; x < src.Width; x++)
                {
                    int i = stride * y + x * 4;
                    int c = assignment[y * src.Width + x];
                    Interlocked.Add(ref sums[c * 4], px[i]);
                    Interlocked.Add(ref sums[c * 4 + 1], px[i + 1]);
                    Interlocked.Add(ref sums[c * 4 + 2], px[i + 2]);
                    Interlocked.Increment(ref sums[c * 4 + 3]);
                }
            });

            bool moved = false;
            for (int c = 0; c < centers.Count; c++)
            {
                long n = sums[c * 4 + 3];
                if (n == 0) continue;
                var nb = (byte)(sums[c * 4] / n);
                var ng = (byte)(sums[c * 4 + 1] / n);
                var nr = (byte)(sums[c * 4 + 2] / n);
                moved |= nb != centers[c].B || ng != centers[c].G || nr != centers[c].R;
                centers[c] = (nb, ng, nr);
            }
            if (!moved) break;
        }

        // Paint quantized colors + stash state.
        Parallel.For(0, src.Height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < src.Width; x++)
            {
                int i = stride * y + x * 4;
                var c = centers[assignment[y * src.Width + x]];
                px[i] = c.B; px[i + 1] = c.G; px[i + 2] = c.R;
            }
        });
        progress?.Report(new StageProgress(Name, 0, 1, 0.95));

        context.Properties[PaletteProperty] = centers.ToArray();
        context.Properties[IndexMapProperty] = assignment;
        return Task.FromResult(context);
    }

    private static List<(byte B, byte G, byte R)> SeedCenters(
        Dictionary<int, (int Count, int B, int G, int R)> histogram, int k)
    {
        var cells = histogram.OrderByDescending(kv => kv.Value.Count).Take(Math.Max(k * 12, 64)).ToList();
        var centers = new List<(byte, byte, byte)>(k);

        static (byte, byte, byte) AvgCell(KeyValuePair<int, (int Count, int B, int G, int R)> cell)
        {
            var e = cell.Value;
            return ((byte)(e.B / e.Count), (byte)(e.G / e.Count), (byte)(e.R / e.Count));
        }

        // First center: dominant cell. Then repeatedly pick the cell farthest from chosen centers.
        var first = cells[0];
        centers.Add(AvgCell(first));

        while (centers.Count < k && cells.Count > centers.Count)
        {
            KeyValuePair<int, (int Count, int B, int G, int R)> best = default;
            int bestDist = -1;
            foreach (var cell in cells)
            {
                var avg = AvgCell(cell);
                int minD = int.MaxValue;
                foreach (var c in centers)
                {
                    int db = avg.Item1 - c.Item1, dg = avg.Item2 - c.Item2, dr = avg.Item3 - c.Item3;
                    int d = db * db + dg * dg + dr * dr;
                    if (d < minD) minD = d;
                }
                // Weight by mass so tiny outliers don't win.
                int score = minD * (int)Math.Sqrt((double)cell.Value.Count);
                if (score > bestDist) { bestDist = score; best = cell; }
            }
            if (bestDist < 0) break;
            centers.Add(AvgCell(best));
        }

        return centers;
    }
}
