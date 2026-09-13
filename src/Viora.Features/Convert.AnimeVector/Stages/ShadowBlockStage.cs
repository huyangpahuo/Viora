using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;

namespace Viora.Features.Convert.AnimeVector.Stages;

/// <summary>
/// Shadow blocking (style rule 4 / §2.5): within each consolidated region, split pixels
/// into a lit tone and one darker shadow tone at a luminance percentile; replace shadow
/// pixels with a hard-edged darkened variant of the region color. Output stays flat —
/// no gradients, only two hard tones per region, matching the reference's blocking.
/// </summary>
public sealed class ShadowBlockStage : StageBase
{
    public override string Name => "ShadowBlock";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        double strength = Dbl(context.Parameters, ParamKeys.Shadows, 0.6); // 0..1
        if (strength <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;
        var assignment = (int[])context.Properties[QuantizeStage.IndexMapProperty]!;

        // Per-color luma histogram to pick the shadow cut (fast, deterministic).
        var centers = ((byte B, byte G, byte R)[])context.Properties[QuantizeStage.PaletteProperty]!;
        var shadowColor = new (byte B, byte G, byte R)[centers.Length];
        for (int c = 0; c < centers.Length; c++)
        {
            var (b, g, r) = centers[c];
            // Hard geometric shade: scale toward black by strength-proportional factor,
            // keep hue (per reference: shadows are darker tints of region color or near-black).
            double f = 1.0 - 0.45 * strength;
            shadowColor[c] = ((byte)(b * f), (byte)(g * f), (byte)(r * f));
        }

        // Compute per-color luma distribution.
        var lumaLists = new List<byte>[centers.Length];
        for (int c = 0; c < centers.Length; c++) lumaLists[c] = new List<byte>(1024);
        for (int i = 0, p = 0; i < assignment.Length; i++, p += 4)
        {
            int luma = (px[p + 2] * 299 + px[p + 1] * 587 + px[p] * 114) / 1000;
            lumaLists[assignment[i]].Add((byte)luma);
        }
        cancellationToken.ThrowIfCancellationRequested();

        // Percentile cut per color (e.g. darkest 30% at strength 0.6).
        var cuts = new int[centers.Length];
        double keptFraction = 0.5 - 0.25 * strength; // stronger → more pixels shadowed
        for (int c = 0; c < centers.Length; c++)
        {
            var list = lumaLists[c];
            if (list.Count == 0) { cuts[c] = int.MaxValue; continue; }
            list.Sort();
            cuts[c] = list[(int)(list.Count * Math.Clamp(keptFraction, 0.05, 0.5))];
        }
        progress?.Report(new StageProgress(Name, 0, 1, 0.4));

        // Global median luma as "light level"; regions darker than half of it are already
        // dark masses (hair shadow, background bands) — don't double-shade (§2.5: decorative
        // but contrast-driven). This preserves contrast structure (style rule 4).
        var allLuma = lumaLists.SelectMany(l => l).OrderBy(l => l).ToArray();
        int medianLuma = allLuma.Length > 0 ? allLuma[allLuma.Length / 2] : 128;

        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            for (int x = 0; x < width; x++)
            {
                int idx = y * width + x;
                int p = y * stride + x * 4;
                int c = assignment[idx];
                int luma = (px[p + 2] * 299 + px[p + 1] * 587 + px[p] * 114) / 1000;

                bool isShadow = luma <= cuts[c] && luma >= medianLuma / 2;
                if (isShadow)
                {
                    var s = shadowColor[c];
                    px[p] = s.B; px[p + 1] = s.G; px[p + 2] = s.R;
                }
            }
        });
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
