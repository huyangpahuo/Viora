using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Region consolidation: connected-component labeling on the index map, then merge
/// regions below the area threshold into their largest neighbor. Produces coherent
/// masses (style rule 3) instead of speckle, with detail parameter controlling threshold.
/// </summary>
public sealed class ConsolidateStage : StageBase
{
    public override string Name => "Consolidate";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double detail = Dbl(context.Parameters, ParamKeys.Detail, 0.5); // 0..1, higher = keep more
        var src = context.Working!;
        int width = src.Width, height = src.Height;
        var assignment = (int[])context.Properties[QuantizeStage.IndexMapProperty]!;

        int minArea = Math.Max(16, (int)((1.0 - detail) * width * height * 0.002));

        // Union-find connected components (4-connectivity, same quantized color index).
        var parent = new int[width * height];
        for (int i = 0; i < parent.Length; i++) parent[i] = i;

        int Find(int x) { while (parent[x] != x) { parent[x] = parent[parent[x]]; x = parent[x]; } return x; }
        void Union(int a, int b) { int ra = Find(a), rb = Find(b); if (ra != rb) parent[rb] = ra; }

        for (int y = 0; y < height; y++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int row = y * width;
            for (int x = 0; x < width; x++)
            {
                int idx = row + x;
                if (x + 1 < width && assignment[idx] == assignment[idx + 1]) Union(idx, idx + 1);
                if (y + 1 < height && assignment[idx] == assignment[idx + width]) Union(idx, idx + width);
            }
        }
        progress?.Report(new StageProgress(Name, 0, 1, 0.3));

        // Component stats.
        var sizes = new Dictionary<int, int>();
        for (int i = 0; i < parent.Length; i++)
        {
            int root = Find(i);
            sizes[root] = sizes.TryGetValue(root, out var n) ? n + 1 : 1;
        }

        // Tiny components: reassign to the most common neighbor color index.
        var newAssignment = new int[width * height];
        Array.Copy(assignment, newAssignment, assignment.Length);
        var mergeTargets = new Dictionary<int, int>(); // root → target color index

        foreach (var (root, size) in sizes)
        {
            if (size >= minArea) continue;

            // Neighbor color histogram.
            var neighborCounts = new Dictionary<int, int>();
            int rootTmp = root;
            // Iterate component pixels via a bounded scan is O(W*H) per component — too slow.
            // Instead single pass below handles all tiny roots at once; skip here.
            _ = rootTmp; _ = neighborCounts;
        }

        // Single pass: for each pixel whose root is tiny, vote for dominant neighboring big-root color.
        var tinyRoots = new HashSet<int>(sizes.Where(kv => kv.Value < minArea).Select(kv => kv.Key));
        if (tinyRoots.Count > 0)
        {
            var votes = new Dictionary<int, Dictionary<int, int>>(); // root → color → count
            for (int y = 0; y < height; y++)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int row = y * width;
                for (int x = 0; x < width; x++)
                {
                    int idx = row + x;
                    int root = Find(idx);
                    if (!tinyRoots.Contains(root)) continue;

                    foreach (var nIdx in Neighbors(x, y, width, height))
                    {
                        int nRoot = Find(nIdx);
                        if (nRoot == root || tinyRoots.Contains(nRoot)) continue;
                        int color = assignment[nIdx];
                        if (!votes.TryGetValue(root, out var vc)) votes[root] = vc = new Dictionary<int, int>();
                        vc[color] = vc.TryGetValue(color, out var c) ? c + 1 : c + 1;
                    }
                }
            }

            var resolve = new Dictionary<int, int>();
            foreach (var (root, vc) in votes)
                resolve[root] = vc.OrderByDescending(kv => kv.Value).First().Key;

            for (int i = 0; i < newAssignment.Length; i++)
            {
                int root = Find(i);
                if (resolve.TryGetValue(root, out var color)) newAssignment[i] = color;
            }
        }
        progress?.Report(new StageProgress(Name, 0, 1, 0.8));

        // Repaint from consolidated map.
        var px = src.Pixels;
        var centers = ((byte B, byte G, byte R)[])context.Properties[QuantizeStage.PaletteProperty]!;
        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int i = y * width + x;
                int p = y * width * 4 + x * 4;
                var c = centers[newAssignment[i]];
                px[p] = c.B; px[p + 1] = c.G; px[p + 2] = c.R;
            }
        });

        context.Properties[QuantizeStage.IndexMapProperty] = newAssignment;
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }

    private static IEnumerable<int> Neighbors(int x, int y, int width, int height)
    {
        if (x > 0) yield return y * width + x - 1;
        if (x + 1 < width) yield return y * width + x + 1;
        if (y > 0) yield return (y - 1) * width + x;
        if (y + 1 < height) yield return (y + 1) * width + x;
    }
}
