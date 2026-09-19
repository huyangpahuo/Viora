using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

/// <summary>Parameter keys for the mosaic preset (single source of truth for VM + stages).</summary>
internal static class MosaicParams
{
    public const string TileSize = "tileSize";
    public const string Grout = "grout";
}

/// <summary>
/// Mosaic preset: tile-average flat colors, dark grout seams and a per-tile ceramic
/// glaze jitter. Two stages, both pure managed and parallel.
/// </summary>
public sealed class MosaicPreset : IStylePreset
{
    public const string PresetId = "builtin.mosaic";

    public string Id => PresetId;

    public string DisplayNameKey => "Preset.Mosaic.Name";

    public string DescriptionKey => "Preset.Mosaic.Description";

    public string? IconGlyph => "\uF0E2"; // Segoe MDL2 GridView

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter(MosaicParams.TileSize, "Param.TileSize", 14, 4, 48, 1),
        new PresetParameter(MosaicParams.Grout, "Param.Grout", 0.25, 0.0, 1.0, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[]
        {
            new PixelateStage(),
            new GroutStage(),
        };
}

/// <summary>Pixelate: replaces each square tile with its average color (the mosaic base).</summary>
public sealed class PixelateStage : StageBase
{
    public override string Name => "Pixelate";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int tile = Math.Clamp(Int(context.Parameters, MosaicParams.TileSize, 14), 2, 128);
        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int tilesX = (width + tile - 1) / tile;
        int tilesY = (height + tile - 1) / tile;

        Parallel.For(0, tilesY, new ParallelOptions { CancellationToken = cancellationToken }, ty =>
        {
            for (int tx = 0; tx < tilesX; tx++)
            {
                int x0 = tx * tile, y0 = ty * tile;
                int x1 = Math.Min(width, x0 + tile);
                int y1 = Math.Min(height, y0 + tile);

                long sb = 0, sg = 0, sr = 0, sa = 0;
                for (int y = y0; y < y1; y++)
                {
                    int row = y * stride;
                    for (int x = x0; x < x1; x++)
                    {
                        int i = row + x * 4;
                        sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; sa += px[i + 3];
                    }
                }

                int count = (x1 - x0) * (y1 - y0);
                byte mb = (byte)(sb / count), mg = (byte)(sg / count);
                byte mr = (byte)(sr / count), ma = (byte)(sa / count);

                for (int y = y0; y < y1; y++)
                {
                    int row = y * stride;
                    for (int x = x0; x < x1; x++)
                    {
                        int i = row + x * 4;
                        px[i] = mb; px[i + 1] = mg; px[i + 2] = mr; px[i + 3] = ma;
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (ty + 1) / (double)tilesY));
        });

        return Task.FromResult(context);
    }
}

/// <summary>
/// Grout: darkens tile seams (deeper toward the crossing point) and applies a
/// deterministic per-tile luma jitter so the tiles read as hand-laid ceramic.
/// </summary>
public sealed class GroutStage : StageBase
{
    public override string Name => "Grout";

    public override Task<ImageProcessingContext> ExecuteAsync(
        ImageProcessingContext context,
        IProgress<StageProgress>? progress,
        CancellationToken cancellationToken)
    {
        int tile = Math.Clamp(Int(context.Parameters, MosaicParams.TileSize, 14), 2, 128);
        double grout = Dbl(context.Parameters, MosaicParams.Grout, 0.25);
        if (grout <= 0.01) return Task.FromResult(context);

        var src = context.Working!;
        int width = src.Width, height = src.Height, stride = src.Stride;
        var px = src.Pixels;

        const int groutB = 62, groutG = 58, groutR = 52; // dark warm gray
        int seam = Math.Max(1, (int)Math.Round(tile * 0.08 * (0.4 + grout)));

        Parallel.For(0, height, new ParallelOptions { CancellationToken = cancellationToken }, y =>
        {
            int ty = y / tile;
            int dy = Math.Min(y % tile, tile - 1 - y % tile);

            for (int x = 0; x < width; x++)
            {
                int tx = x / tile;
                int dx = Math.Min(x % tile, tile - 1 - x % tile);
                int d = Math.Min(dx, dy);
                int i = y * stride + x * 4;

                if (d < seam)
                {
                    // mix toward grout color; strongest at the seam center
                    double t = grout * (1.0 - 0.6 * d / (double)seam);
                    px[i] = PixelOps.Clamp((int)(px[i] + (groutB - px[i]) * t));
                    px[i + 1] = PixelOps.Clamp((int)(px[i + 1] + (groutG - px[i + 1]) * t));
                    px[i + 2] = PixelOps.Clamp((int)(px[i + 2] + (groutR - px[i + 2]) * t));
                }
                else
                {
                    // deterministic per-tile glaze jitter (hash of tile coords)
                    int jitter = (((tx * 73856093) ^ (ty * 19349663)) % 21) - 10;
                    int j = (int)(jitter * grout * 0.35);
                    px[i] = PixelOps.Clamp(px[i] + j);
                    px[i + 1] = PixelOps.Clamp(px[i + 1] + j);
                    px[i + 2] = PixelOps.Clamp(px[i + 2] + j);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)height));
        });

        return Task.FromResult(context);
    }
}
