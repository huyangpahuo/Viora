using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Group C: handcraft & material styles ============
// Clay / Collage / Embroidery / Knitted / StainedGlass / Porcelain / Origami / Chalkboard / WaxCrayon.

/// <summary>黏土雕塑:圆润体积 + 手工模型感。</summary>
public sealed class ClayPreset : IStylePreset
{
    public string Id => "builtin.clay";
    public string DisplayNameKey => "Preset.Clay.Name";
    public string DescriptionKey => "Preset.Clay.Description";
    public string? IconGlyph => "\uE7F1";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("smoothing", "Param.Generic.Intensity", 0.7, 0.0, 1.0, 0.05),
        new PresetParameter("relief", "Param.Generic.Texture", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ClayStage() };
}

public sealed class ClayStage : StageBase
{
    public override string Name => "Clay";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double smoothing = Dbl(context.Parameters, "smoothing", 0.7);
        double relief = Dbl(context.Parameters, "relief", 0.5);

        // Heavy color smoothing gives the flattened, thumb-pressed surface;
        // the original still bleeds through so hues survive.
        var flat = ImageOps.BoxBlurColor(px, stride, w, h, 3 + (int)(smoothing * 9));

        var luma = ImageOps.LumaMap(src);
        var soft = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(soft, w, h, 2 + (int)(smoothing * 5));

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int f = y * stride + x * 4;

                    // clay = mostly flattened color, a little original detail
                    double mix = 0.62;
                    int b = (int)(flat[f] * mix + px[i] * (1 - mix));
                    int g = (int)(flat[f + 1] * mix + px[i + 1] * (1 - mix));
                    int r = (int)(flat[f + 2] * mix + px[i + 2] * (1 - mix));

                    // matte pastel lift: pull toward milky tone
                    b = b + (150 - b) * 14 / 100;
                    g = g + (148 - g) * 14 / 100;
                    r = r + (155 - r) * 14 / 100;

                    // rounded-volume shading from the blurred vertical slope
                    double slope = (soft[Math.Min(h - 1, y + 1) * w + x] - soft[Math.Max(0, y - 1) * w + x]) / 255.0;
                    double shade = 1.0 + slope * relief * 2.0;

                    px[i] = ImageOps.Clamp((int)(b * shade));
                    px[i + 1] = ImageOps.Clamp((int)(g * shade));
                    px[i + 2] = ImageOps.Clamp((int)(r * shade));
                }
                progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
            }
        }, ct);

        return context;
    }
}

/// <summary>拼贴艺术:素材撕贴组合。</summary>
public sealed class CollagePreset : IStylePreset
{
    public string Id => "builtin.collage";
    public string DisplayNameKey => "Preset.Collage.Name";
    public string DescriptionKey => "Preset.Collage.Description";
    public string? IconGlyph => "\uE8B2";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("tiles", "Param.Generic.Density", 4, 2, 6, 1),
        new PresetParameter("rotation", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new CollageStage() };
}

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

/// <summary>刺绣:线迹针脚构成图像。</summary>
public sealed class EmbroideryPreset : IStylePreset
{
    public string Id => "builtin.embroidery";
    public string DisplayNameKey => "Preset.Embroidery.Name";
    public string DescriptionKey => "Preset.Embroidery.Description";
    public string? IconGlyph => "\uE745";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("stitch", "Param.Generic.Size", 6, 3, 16, 1),
        new PresetParameter("colors", "Param.Generic.Colors", 8, 4, 16, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new EmbroideryStage() };
}

public sealed class EmbroideryStage : StageBase
{
    public override string Name => "Embroidery";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int stitch = Math.Clamp(Int(context.Parameters, "stitch", 6), 3, 24);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 8), 2, 24);

        var palette = ImageOps.ExtractPalette(src, colors);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // thread sheen: per-stitch cell diagonal running bond
                int cellX = x / stitch, cellY = y / stitch;
                double angle = ((cellX + cellY) & 1) == 0 ? Math.PI / 4 : -Math.PI / 4;
                double along = Math.Cos(angle) * (x % stitch) + Math.Sin(angle) * (y % stitch);
                double sheen = 0.78 + 0.42 * (0.5 + 0.5 * Math.Sin(along * (Math.PI / Math.Max(2, stitch / 2.0))));

                // fabric weave between stitches
                double weave = 1.0 + (ImageOps.Hash(x, y, 19) - 0.5) * 0.10;

                px[i] = ImageOps.Clamp((int)(p.B * sheen * weave));
                px[i + 1] = ImageOps.Clamp((int)(p.G * sheen * weave));
                px[i + 2] = ImageOps.Clamp((int)(p.R * sheen * weave));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>针织:毛线纹理 + 编织结构。</summary>
public sealed class KnittedPreset : IStylePreset
{
    public string Id => "builtin.knitted";
    public string DisplayNameKey => "Preset.Knitted.Name";
    public string DescriptionKey => "Preset.Knitted.Description";
    public string? IconGlyph => "\uE719";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("stitchSize", "Param.Generic.Size", 10, 5, 24, 1),
        new PresetParameter("colors", "Param.Generic.Colors", 6, 3, 12, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new KnittedStage() };
}

public sealed class KnittedStage : StageBase
{
    public override string Name => "Knitted";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int size = Math.Clamp(Int(context.Parameters, "stitchSize", 10), 4, 32);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 6), 2, 16);

        var palette = ImageOps.ExtractPalette(src, colors);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // one knit "V" per cell: two crossed lobes
                double lx = (x % (double)size) / size - 0.5;
                double ly = (y % (double)size) / (double)size;
                double lobe = Math.Exp(-Math.Pow(Math.Abs(lx) * 2 - ly * 0.8, 2) * 5.0);
                double shade = 0.62 + 0.55 * Math.Clamp(lobe, 0, 1);

                // darker inter-loop shadow
                if (ly < 0.14) shade *= 0.55;

                double fuzz = 1.0 + (ImageOps.Hash(x, y, 23) - 0.5) * 0.12;
                px[i] = ImageOps.Clamp((int)(p.B * shade * fuzz));
                px[i + 1] = ImageOps.Clamp((int)(p.G * shade * fuzz));
                px[i + 2] = ImageOps.Clamp((int)(p.R * shade * fuzz));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>彩色玻璃:玻璃色块 + 铅条分割。</summary>
public sealed class StainedGlassPreset : IStylePreset
{
    public string Id => "builtin.stained-glass";
    public string DisplayNameKey => "Preset.StainedGlass.Name";
    public string DescriptionKey => "Preset.StainedGlass.Description";
    public string? IconGlyph => "\uEA3A";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 20, 8, 48, 2),
        new PresetParameter("lead", "Param.Generic.Thickness", 2, 1, 6, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new StainedGlassStage() };
}

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

/// <summary>瓷器:釉面反光 + 精致质感。</summary>
public sealed class PorcelainPreset : IStylePreset
{
    public string Id => "builtin.porcelain";
    public string DisplayNameKey => "Preset.Porcelain.Name";
    public string DescriptionKey => "Preset.Porcelain.Description";
    public string? IconGlyph => "\uE80A";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("gloss", "Param.Generic.Glow", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("smoothing", "Param.Generic.Intensity", 0.7, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PorcelainStage() };
}

public sealed class PorcelainStage : StageBase
{
    public override string Name => "Porcelain";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double gloss = Dbl(context.Parameters, "gloss", 0.5);
        double smoothing = Dbl(context.Parameters, "smoothing", 0.7);

        int radius = 2 + (int)(smoothing * 6);

        // Actually-blurred color copy (the glaze base) + soft luma for the sheen mask.
        var glaze = ImageOps.BoxBlurColor(px, stride, w, h, radius);
        var luma = ImageOps.LumaMap(src);
        var soft = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(soft, w, h, Math.Max(1, radius / 2));

        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                int f = y * stride + x * 4;
                double t = soft[y * w + x] / 255.0;

                // cool glazed white carries most of the tone; image keeps a third
                double b = glaze[f] * 0.34 + 238 * 0.66;
                double g = glaze[f + 1] * 0.34 + 243 * 0.66;
                double r = glaze[f + 2] * 0.34 + 246 * 0.66;

                // specular glaze: bright zones get a hard highlight, curves get sheen
                double spec = Math.Pow(Math.Max(0, t - 0.52) / 0.48, 1.6) * gloss;
                double rim = Math.Pow(1 - t, 2.2) * 0.10 * gloss;
                r += 235 * spec + 235 * rim;
                g += 238 * spec + 238 * rim;
                b += 242 * spec + 242 * rim;

                px[i] = ImageOps.Clamp((int)b);
                px[i + 1] = ImageOps.Clamp((int)g);
                px[i + 2] = ImageOps.Clamp((int)r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        }

        return Task.FromResult(context);
    }
}

/// <summary>折纸:折痕结构 + 几何立体。</summary>
public sealed class OrigamiPreset : IStylePreset
{
    public string Id => "builtin.origami";
    public string DisplayNameKey => "Preset.Origami.Name";
    public string DescriptionKey => "Preset.Origami.Description";
    public string? IconGlyph => "\uE8A5";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 28, 12, 64, 2),
        new PresetParameter("crease", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new OrigamiStage() };
}

public sealed class OrigamiStage : StageBase
{
    public override string Name => "Origami";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 28), 8, 96);
        double crease = Dbl(context.Parameters, "crease", 0.5);

        // Folded-paper facets read from a smoothed copy of the picture.
        var smooth = ImageOps.BoxBlurColor(src.Pixels, stride, w, h, Math.Clamp(cell / 4, 1, 8));

        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;
        int cols = (w + cell - 1) / cell + 1, rows = (h + cell - 1) / cell + 1;
        var points = new (double X, double Y)[cols, rows];
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                double jx = (ImageOps.Hash(gx, gy, 41) - 0.5) * cell * 0.5;
                double jy = (ImageOps.Hash(gx, gy, 43) - 0.5) * cell * 0.5;
                points[gx, gy] = (gx * (double)cell - jx, gy * (double)cell - jy);
            }

        for (int gy = 0; gy < rows - 1; gy++)
        {
            for (int gx = 0; gx < cols - 1; gx++)
            {
                var p00 = points[gx, gy]; var p10 = points[gx + 1, gy];
                var p01 = points[gx, gy + 1]; var p11 = points[gx + 1, gy + 1];
                var diag = (X: (p00.X + p11.X) / 2, Y: (p00.Y + p11.Y) / 2);

                var tris = new ((double X, double Y) A, (double X, double Y) B, (double X, double Y) C)[]
                {
                    (p00, p10, diag), (p00, diag, p01), (p10, p11, diag), (p01, diag, p11),
                };

                int triId = gx * 4 + gy * 97;
                foreach (var (a, b, c) in tris)
                {
                    double cx = (a.X + b.X + c.X) / 3, cy = (a.Y + b.Y + c.Y) / 3;
                    int sx = Math.Clamp((int)cx, 0, w - 1), sy = Math.Clamp((int)cy, 0, h - 1);
                    int pi = sy * stride + sx * 4;

                    // fold shading: each facet tilts toward/away from the light
                    double fold = 0.86 + ImageOps.Hash(gx * 2 + (triId++ & 1), gy, 67) * (0.10 + crease * 0.22);

                    int bB = ImageOps.Clamp((int)((smooth[pi] * 0.45 + 240 * 0.55) * fold));
                    int bG = ImageOps.Clamp((int)((smooth[pi + 1] * 0.45 + 236 * 0.55) * fold));
                    int bR = ImageOps.Clamp((int)((smooth[pi + 2] * 0.45 + 230 * 0.55) * fold));
                    ImageOps.FillTriangle(outPx, outStride, w, h, a, b, c, bB, bG, bR, shadeEdges: false);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (gy + 1) / (double)(rows - 1)));
        }

        // paper grain over the facets
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                double n = (ImageOps.Hash(x, y, 47) - 0.5) * (6 + 14 * crease);
                byte add = ImageOps.Clamp((int)(n));
                outPx[i] = ImageOps.Clamp(outPx[i] + add);
                outPx[i + 1] = ImageOps.Clamp(outPx[i + 1] + add);
                outPx[i + 2] = ImageOps.Clamp(outPx[i + 2] + add);
            }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>黑板粉笔:粉笔颗粒 + 教学感。</summary>
public sealed class ChalkboardPreset : IStylePreset
{
    public string Id => "builtin.chalkboard";
    public string DisplayNameKey => "Preset.Chalkboard.Name";
    public string DescriptionKey => "Preset.Chalkboard.Description";
    public string? IconGlyph => "\uE7C3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("dust", "Param.Generic.Texture", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ChalkboardStage() };
}

public sealed class ChalkboardStage : StageBase
{
    public override string Name => "Chalkboard";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double detail = Dbl(context.Parameters, "detail", 0.5);
        double dust = Dbl(context.Parameters, "dust", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = (1f - (float)detail) * 180f + 50f;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // slate board with smudge noise
                int b = 34, g = 48, r = 38;
                double smudge = ImageOps.Hash(x / 6, y / 6, 51);
                if (smudge > 0.8)
                {
                    b += 8; g += 10; r += 8;
                }

                // bright edges become chalk strokes with per-pixel chalk gaps
                double t = mag[y * w + x] / threshold;
                if (t > 1.0)
                {
                    double coverage = Math.Min(1.0, (t - 1.0) * 1.2) *
                                      (0.55 + 0.45 * ImageOps.Hash(x, y, 53));
                    double d = dust * ImageOps.Hash(x + 11, y + 7, 59) * 0.5;
                    double k = Math.Min(1.0, coverage + d * 0.3);
                    b = (int)(b + (225 - b) * k);
                    g = (int)(g + (232 - g) * k);
                    r = (int)(r + (228 - r) * k);
                }

                px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>蜡笔:蜡质笔触 + 童趣涂抹。</summary>
public sealed class WaxCrayonPreset : IStylePreset
{
    public string Id => "builtin.wax-crayon";
    public string DisplayNameKey => "Preset.WaxCrayon.Name";
    public string DescriptionKey => "Preset.WaxCrayon.Description";
    public string? IconGlyph => "\uE7C3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("stroke", "Param.Generic.Size", 8, 3, 20, 1),
        new PresetParameter("pressure", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new WaxCrayonStage() };
}

public sealed class WaxCrayonStage : StageBase
{
    public override string Name => "WaxCrayon";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int stroke = Math.Clamp(Int(context.Parameters, "stroke", 8), 3, 24);
        double pressure = Dbl(context.Parameters, "pressure", 0.6);

        var palette = ImageOps.ExtractPalette(src, 6);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[i], original[i + 1], original[i + 2])];

                // wax skip: coverage from stroke-direction streaks + grain
                double streak = 0.5 + 0.5 * Math.Sin((y + (x / (double)stroke) * stroke * 0.7) * 2.2);
                double grain = ImageOps.Hash(x, y, 61);
                double coverage = Math.Clamp(pressure * (0.35 + 0.5 * streak) + (grain - 0.5) * 0.5, 0, 1);

                int paperB = 248, paperG = 245, paperR = 238;
                px[i] = ImageOps.Clamp((int)(paperB * (1 - coverage) + p.B * coverage));
                px[i + 1] = ImageOps.Clamp((int)(paperG * (1 - coverage) + p.G * coverage));
                px[i + 2] = ImageOps.Clamp((int)(paperR * (1 - coverage) + p.R * coverage));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}
