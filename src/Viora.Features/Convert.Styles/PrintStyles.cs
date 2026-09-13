using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Group A: print & drawing styles ============
// Comic / PixelArt / LowPoly / PaperCut / Woodcut / Risograph / Halftone / Newspaper / Blueprint.
// Each preset = metadata + one fused stage; params reuse generic label keys.

/// <summary>漫画:粗黑描边 + 海报化色块 + 硬朗阴影。</summary>
public sealed class ComicPreset : IStylePreset
{
    public string Id => "builtin.comic";
    public string DisplayNameKey => "Preset.Comic.Name";
    public string DescriptionKey => "Preset.Comic.Description";
    public string? IconGlyph => "\uE90C";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("colors", "Param.Generic.Colors", 5, 3, 8, 1),
        new PresetParameter("thickness", "Param.Generic.Thickness", 2, 1, 4, 1),
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ComicStage() };
}

public sealed class ComicStage : StageBase
{
    public override string Name => "Comic";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;

        int colors = Math.Clamp(Int(context.Parameters, "colors", 5), 3, 8);
        int thickness = Math.Clamp(Int(context.Parameters, "thickness", 2), 1, 4);
        double detail = Dbl(context.Parameters, "detail", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);

        var poster = ImageOps.PosterizeLut(colors);
        float threshold = (1f - (float)detail) * 160f + 40f;

        // mild blur pass for clean flats
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                    px[i + c] = poster[px[i + c]];

                // thick ink on strong edges (dilated via neighborhood max)
                bool ink = false;
                for (int k = 0; k < thickness && !ink; k++)
                    for (int dy = -k; dy <= k && !ink; dy++)
                        for (int dx = -k; dx <= k && !ink; dx++)
                        {
                            int nx = Math.Clamp(x + dx, 0, w - 1), ny = Math.Clamp(y + dy, 0, h - 1);
                            if (mag[ny * w + nx] > threshold) ink = true;
                        }
                if (ink)
                {
                    px[i] = 30; px[i + 1] = 26; px[i + 2] = 34;
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>像素艺术:低分辨率重构 + 有限色板。</summary>
public sealed class PixelArtPreset : IStylePreset
{
    public string Id => "builtin.pixel-art";
    public string DisplayNameKey => "Preset.PixelArt.Name";
    public string DescriptionKey => "Preset.PixelArt.Description";
    public string? IconGlyph => "\uE7F4";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("pixelSize", "Param.Generic.Size", 8, 4, 32, 1),
        new PresetParameter("colors", "Param.Generic.Colors", 16, 4, 32, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PixelArtStage() };
}

public sealed class PixelArtStage : StageBase
{
    public override string Name => "PixelArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        int tile = Math.Clamp(Int(context.Parameters, "pixelSize", 8), 2, 64);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 16), 2, 48);

        var px = src.Pixels;
        var palette = ImageOps.ExtractPalette(src, colors);

        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int cols = (w + tile - 1) / tile, rows = (h + tile - 1) / tile;
        var cellColor = new int[cols * rows];

        // tile average
        for (int ty = 0; ty < rows; ty++)
            for (int tx = 0; tx < cols; tx++)
            {
                long sb = 0, sg = 0, sr = 0; int n = 0;
                int x1 = Math.Min(w, (tx + 1) * tile), y1 = Math.Min(h, (ty + 1) * tile);
                for (int y = ty * tile; y < y1; y++)
                    for (int x = tx * tile; x < x1; x++)
                    {
                        int i = y * stride + x * 4;
                        sb += px[i]; sg += px[i + 1]; sr += px[i + 2]; n++;
                    }
                cellColor[ty * cols + tx] = ImageOps.NearestPaletteIndex(palette,
                    (int)(sb / n), (int)(sg / n), (int)(sr / n));
            }

        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                var p = palette[cellColor[(y / tile) * cols + x / tile]];
                int i = y * dst.Stride + x * 4;
                outPx[i] = (byte)p.B; outPx[i + 1] = (byte)p.G; outPx[i + 2] = (byte)p.R; outPx[i + 3] = 255;
            }
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>低多边形:三角面切割 + 几何光影。</summary>
public sealed class LowPolyPreset : IStylePreset
{
    public string Id => "builtin.low-poly";
    public string DisplayNameKey => "Preset.LowPoly.Name";
    public string DescriptionKey => "Preset.LowPoly.Description";
    public string? IconGlyph => "\uF0E4";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 24, 10, 64, 2),
        new PresetParameter("jitter", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new LowPolyStage() };
}

public sealed class LowPolyStage : StageBase
{
    public override string Name => "LowPoly";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 24), 6, 96);
        double jitter = Dbl(context.Parameters, "jitter", 0.6);

        // Facet colors come from a blurred copy — single-pixel sampling makes facets noisy.
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
                double jx = (ImageOps.Hash(gx, gy, 1) - 0.5) * cell * jitter;
                double jy = (ImageOps.Hash(gx, gy, 2) - 0.5) * cell * jitter;
                points[gx, gy] = (gx * (double)cell - jx, gy * (double)cell - jy);
            }

        await Task.Run(() =>
        {
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

                    foreach (var (a, b, c) in tris)
                    {
                        double cx = (a.X + b.X + c.X) / 3, cy = (a.Y + b.Y + c.Y) / 3;
                        int sx = Math.Clamp((int)cx, 0, w - 1), sy = Math.Clamp((int)cy, 0, h - 1);
                        int i = sy * stride + sx * 4;
                        ImageOps.FillTriangle(outPx, outStride, w, h, a, b, c, smooth[i], smooth[i + 1], smooth[i + 2], shadeEdges: false);
                    }
                }
                progress?.Report(new StageProgress(Name, 0, 1, (gy + 1) / (double)(rows - 1)));
            }
        }, ct);

        context.Working = dst;
        return context;
    }
}

/// <summary>剪纸:多层纸片 + 叠层阴影。</summary>
public sealed class PaperCutPreset : IStylePreset
{
    public string Id => "builtin.paper-cut";
    public string DisplayNameKey => "Preset.PaperCut.Name";
    public string DescriptionKey => "Preset.PaperCut.Description";
    public string? IconGlyph => "\uE8D9";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("layers", "Param.Generic.Colors", 5, 3, 8, 1),
        new PresetParameter("offset", "Param.Generic.Offset", 2, 1, 6, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PaperCutStage() };
}

public sealed class PaperCutStage : StageBase
{
    public override string Name => "PaperCut";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int layers = Math.Clamp(Int(context.Parameters, "layers", 5), 2, 8);
        int off = Math.Clamp(Int(context.Parameters, "offset", 2), 1, 8);

        var poster = ImageOps.PosterizeLut(layers);
        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                    px[i + c] = ImageOps.Clamp((int)(poster[px[i + c]] * 1.08)); // bright paper tones

                // shadow: this pixel sits on a higher layer than the one behind (offset sample)
                int sx = Math.Max(0, x - off), sy = Math.Max(0, y - off);
                if (luma[y * w + x] > luma[sy * w + sx] + 8)
                {
                    px[i] = ImageOps.Clamp(px[i] - 70);
                    px[i + 1] = ImageOps.Clamp(px[i + 1] - 64);
                    px[i + 2] = ImageOps.Clamp(px[i + 2] - 56);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>木刻版画:雕刻线条 + 高反差。</summary>
public sealed class WoodcutPreset : IStylePreset
{
    public string Id => "builtin.woodcut";
    public string DisplayNameKey => "Preset.Woodcut.Name";
    public string DescriptionKey => "Preset.Woodcut.Description";
    public string? IconGlyph => "\uEDA0";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.7, 0.0, 1.0, 0.05),
        new PresetParameter("density", "Param.Generic.Density", 0.6, 0.1, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new WoodcutStage() };
}

public sealed class WoodcutStage : StageBase
{
    public override string Name => "Woodcut";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.7);
        double density = Dbl(context.Parameters, "density", 0.6);

        var contrastLut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 2);

        const byte paperB = 236, paperG = 230, paperR = 214;
        const byte ink = 26;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                double t = 1.0 - blurred[y * w + x] / 255.0; // darkness
                double period = 3.0 + (1.0 - t) * 14.0 / Math.Max(0.15, density);
                double linePos = (y + 3.0 * Math.Sin(x * 0.06)) % period;
                linePos = linePos < 0 ? linePos + period : linePos;
                double cut = period * t * 0.75;
                double v = linePos < cut ? 1.0 : 0.0;

                byte l = contrastLut[luma[y * w + x]];
                double k = v * 0.9 + (255 - l) / 255.0 * 0.1; // keep some tone in highlights
                px[i] = ImageOps.Clamp((int)(paperB + (ink - paperB) * k));
                px[i + 1] = ImageOps.Clamp((int)(paperG + (ink - paperG) * k));
                px[i + 2] = ImageOps.Clamp((int)(paperR + (ink - paperR) * k));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>孔版印刷:双色油墨 + 套色错位 + 颗粒。</summary>
public sealed class RisographPreset : IStylePreset
{
    public string Id => "builtin.risograph";
    public string DisplayNameKey => "Preset.Risograph.Name";
    public string DescriptionKey => "Preset.Risograph.Description";
    public string? IconGlyph => "\uE8FD";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("offset", "Param.Generic.Offset", 2, 0, 6, 1),
        new PresetParameter("grain", "Param.Generic.Grain", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new RisographStage() };
}

public sealed class RisographStage : StageBase
{
    public override string Name => "Risograph";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int off = Int(context.Parameters, "offset", 2);
        double grain = Dbl(context.Parameters, "grain", 0.5);

        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        const byte paperB = 243, paperG = 240, paperR = 226;
        const byte inkAB = 122, inkAG = 44, inkAR = 226;   // riso blue-ish
        const byte inkBB = 84, inkBG = 58, inkBR = 240;    // riso red-pink

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = 1.0 - blurred[y * w + x] / 255.0;
                double n = (ImageOps.Hash(x, y, 7) - 0.5) * grain * 0.35;

                int xa = Math.Clamp(x + off, 0, w - 1);          // blue plate shifted
                int xb = Math.Clamp(x - off, 0, w - 1);          // red plate shifted
                double ta = 1.0 - blurred[y * w + xa] / 255.0;
                double tb = 1.0 - blurred[y * w + xb] / 255.0;

                double a = ta > 0.52 ? 1.0 : 0.0;
                double b = tb > 0.72 ? 1.0 : 0.0;

                double r = paperR, g = paperG, bl = paperB;
                if (a > 0) { r = inkAR; g = inkAG; bl = inkAB; }
                if (b > 0) { r = inkBR; g = inkBG; bl = inkBB; }
                if (a > 0 && b > 0) { r = 210; g = 30; bl = 140; } // overprint violet

                double f = 1.0 + n;
                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp((int)(bl * f));
                px[i + 1] = ImageOps.Clamp((int)(g * f));
                px[i + 2] = ImageOps.Clamp((int)(r * f));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>半色调:印刷网点明暗。</summary>
public sealed class HalftonePreset : IStylePreset
{
    public string Id => "builtin.halftone";
    public string DisplayNameKey => "Preset.Halftone.Name";
    public string DescriptionKey => "Preset.Halftone.Description";
    public string? IconGlyph => "\uE7A8";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 8, 4, 24, 1),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new HalftoneStage() };
}

public sealed class HalftoneStage : StageBase
{
    public override string Name => "Halftone";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 8), 3, 32);
        double contrast = Dbl(context.Parameters, "contrast", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, Math.Max(1, cell / 3));

        // Paper keeps a lightened trace of the original color; ink dots carry the tone.
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp(px[i] * 35 / 100 + 220);
                px[i + 1] = ImageOps.Clamp(px[i + 1] * 35 / 100 + 220);
                px[i + 2] = ImageOps.Clamp(px[i + 2] * 35 / 100 + 218);
            }
        });

        // Staggered dot screen (odd rows shifted half a cell) sized by cell-average luma.
        int rows = (h + cell - 1) / cell;
        for (int cy = 0; cy < rows; cy++)
        {
            double stagger = (cy & 1) == 1 ? cell * 0.5 : 0.0;
            int cols = (w + cell - 1) / cell + 1;
            for (int cx = 0; cx < cols; cx++)
            {
                int ccx = (int)(cx * cell + cell / 2.0 + stagger - cell * 0.5);
                int ccy = cy * cell + cell / 2;
                int sx = Math.Clamp(ccx, 0, w - 1), sy = Math.Clamp(ccy, 0, h - 1);

                // average the luma over the cell footprint
                long sum = 0; int n = 0;
                int x1 = Math.Min(w, ccx + cell), y1 = Math.Min(h, ccy + cell);
                for (int yy = Math.Max(0, ccy); yy < y1; yy++)
                    for (int xx = Math.Max(0, ccx); xx < x1; xx++)
                    {
                        sum += lut[blurred[yy * w + xx]]; n++;
                    }
                double t = n > 0 ? sum / (double)(n * 255) : 1.0;

                double radius = cell * 0.68 * Math.Sqrt(1.0 - t);
                if (radius < 0.4) continue;
                ImageOps.FillCircle(px, stride, w, h, ccx + 0.5, ccy + 0.5, radius, 24, 22, 26);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (cy + 1) / (double)rows));
        }

        return Task.FromResult(context);
    }
}

/// <summary>报纸印刷:新闻纸 + 黑白油墨。</summary>
public sealed class NewspaperPreset : IStylePreset
{
    public string Id => "builtin.newspaper";
    public string DisplayNameKey => "Preset.Newspaper.Name";
    public string DescriptionKey => "Preset.Newspaper.Description";
    public string? IconGlyph => "\uE900";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("grain", "Param.Generic.Grain", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new NewspaperStage() };
}

public sealed class NewspaperStage : StageBase
{
    public override string Name => "Newspaper";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.6);
        double grain = Dbl(context.Parameters, "grain", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double v = lut[luma[y * w + x]] / 255.0;
                // fine print-dot texture
                double dot = ((x + y) % 3 == 0) ? 0.94 : 1.0;
                double n = 1.0 + (ImageOps.Hash(x, y, 11) - 0.5) * grain * 0.22;
                double ink = 1.0 - v * dot * n;

                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp((int)(228 - ink * 205));
                px[i + 1] = ImageOps.Clamp((int)(225 - ink * 202));
                px[i + 2] = ImageOps.Clamp((int)(214 - ink * 190));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>蓝图:工程线稿 + 网格底。</summary>
public sealed class BlueprintPreset : IStylePreset
{
    public string Id => "builtin.blueprint";
    public string DisplayNameKey => "Preset.Blueprint.Name";
    public string DescriptionKey => "Preset.Blueprint.Description";
    public string? IconGlyph => "\uF0F7";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("grid", "Param.Generic.Density", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new BlueprintStage() };
}

public sealed class BlueprintStage : StageBase
{
    public override string Name => "Blueprint";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double detail = Dbl(context.Parameters, "detail", 0.5);
        double grid = Dbl(context.Parameters, "grid", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = (1f - (float)detail) * 200f + 60f;
        int gridStep = 40;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // drafting-paper blue with subtle vignette
                double vig = 1.0 - 0.18 * Math.Abs((x / (double)w - 0.5)) * 2 * Math.Abs((y / (double)h - 0.5)) * 2;
                int b = (int)(150 * vig), g = (int)(64 * vig), r = (int)(24 * vig);

                bool onGrid = (x % gridStep == 0) || (y % gridStep == 0);
                if (onGrid && grid > 0.01)
                {
                    b += (int)(40 * grid); g += (int)(28 * grid); r += (int)(18 * grid);
                }

                if (mag[y * w + x] > threshold)
                {
                    b = 235; g = 244; r = 255; // white ink lines
                }

                px[i] = (byte)Math.Min(255, b);
                px[i + 1] = (byte)Math.Min(255, g);
                px[i + 2] = (byte)Math.Min(255, r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}
