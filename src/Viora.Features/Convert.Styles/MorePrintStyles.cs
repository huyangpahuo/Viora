using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Styles 31-38 + 49-50: print / engraving / dither / relief ============

/// <summary>点描:密集大小不同的圆点塑造明暗。</summary>
public sealed class StipplingPreset : IStylePreset
{
    public string Id => "builtin.stippling";
    public string DisplayNameKey => "Preset.Stippling.Name";
    public string DescriptionKey => "Preset.Stippling.Description";
    public string? IconGlyph => "\uE95D";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("density", "Param.Generic.Density", 0.6, 0.1, 1.0, 0.05),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.4, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new StipplingStage() };
}

public sealed class StipplingStage : StageBase
{
    public override string Name => "Stippling";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double density = Dbl(context.Parameters, "density", 0.6);
        double contrast = Dbl(context.Parameters, "contrast", 0.4);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 2);

        int cell = (int)Math.Round(5.5 - density * 3.0); // denser → smaller cells
        int rows = (h + cell - 1) / cell;

        // paper base first — only stipple dots should carry the image
        for (int i = 0; i < px.Length; i += 4)
        {
            px[i] = 243; px[i + 1] = 240; px[i + 2] = 233;
        }

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < (w + cell - 1) / cell; cx++)
            {
                int sx = Math.Min(w - 1, cx * cell + cell / 2);
                int sy = Math.Min(h - 1, cy * cell + cell / 2);
                double darkness = 1.0 - lut[blurred[sy * w + sx]] / 255.0;
                int want = (int)Math.Round(darkness * 3.2 * (0.4 + density));

                for (int k = 0; k < want; k++)
                {
                    double jx = ImageOps.Hash(cx, cy * 8 + k, 71);
                    double jy = ImageOps.Hash(cx * 8 + k, cy, 73);
                    double radius = 0.45 + darkness * 1.1 + jx * 0.35;
                    double ddx = cx * cell + jx * cell;
                    double ddy = cy * cell + jy * cell;
                    if (ddx >= w || ddy >= h) continue;
                    ImageOps.FillCircle(px, stride, w, h, ddx, ddy, radius, 34, 30, 28);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (cy + 1) / (double)rows));
        }
        return Task.FromResult(context);
    }
}

/// <summary>交叉排线:多方向线条叠加形成阴影。</summary>
public sealed class CrossHatchingPreset : IStylePreset
{
    public string Id => "builtin.cross-hatching";
    public string DisplayNameKey => "Preset.CrossHatching.Name";
    public string DescriptionKey => "Preset.CrossHatching.Description";
    public string? IconGlyph => "\uE7C5";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("density", "Param.Generic.Density", 0.6, 0.1, 1.0, 0.05),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new CrossHatchingStage() };
}

public sealed class CrossHatchingStage : StageBase
{
    public override string Name => "CrossHatching";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double density = Dbl(context.Parameters, "density", 0.6);
        double contrast = Dbl(context.Parameters, "contrast", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        int spacing = Math.Max(2, (int)Math.Round(9 - density * 6));

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = lut[blurred[y * w + x]] / 255.0; // brightness
                double layers = (1.0 - t) * 4.0;            // 0..4 hatch layers
                double line = 0;
                if (layers > 0.5 && (x + y) % (spacing * 2) < 2) line = 1;                      // 45°
                if (layers > 1.5 && (x - y + 4 * w) % (spacing * 2) < 2) line = 1;              // -45°
                if (layers > 2.5 && y % spacing < 2) line = 1;                                  // horizontal
                if (layers > 3.5 && x % spacing < 2) line = 1;                                  // vertical

                int i = y * stride + x * 4;
                const int paperB = 246, paperG = 243, paperR = 235;
                const int inkB = 38, inkG = 34, inkR = 32;
                px[i] = ImageOps.Clamp((int)(paperB + (inkB - paperB) * line));
                px[i + 1] = ImageOps.Clamp((int)(paperG + (inkG - paperG) * line));
                px[i + 2] = ImageOps.Clamp((int)(paperR + (inkR - paperR) * line));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>蚀刻版画:极细密的雕刻线 + 金属版画质感。</summary>
public sealed class EtchingPreset : IStylePreset
{
    public string Id => "builtin.etching";
    public string DisplayNameKey => "Preset.Etching.Name";
    public string DescriptionKey => "Preset.Etching.Description";
    public string? IconGlyph => "\uE8F3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("density", "Param.Generic.Density", 0.7, 0.2, 1.0, 0.05),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.4, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new EtchingStage() };
}

public sealed class EtchingStage : StageBase
{
    public override string Name => "Etching";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double density = Dbl(context.Parameters, "density", 0.7);
        double contrast = Dbl(context.Parameters, "contrast", 0.4);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        double spacing = Math.Max(2.0, 5.0 - density * 3.0);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = 1.0 - lut[blurred[y * w + x]] / 255.0; // darkness
                double wavy = y + Math.Sin(x * 0.045 + y * 0.01) * 0.9;
                double pos = wavy % spacing;
                if (pos < 0) pos += spacing;
                double cut = spacing * Math.Pow(t, 1.25);
                double line = Math.Clamp((cut - pos) / 1.2, 0, 1); // sub-pixel AA on the groove

                int i = y * stride + x * 4;
                const int paperB = 232, paperG = 226, paperR = 210;
                const int inkB = 42, inkG = 38, inkR = 34;
                px[i] = ImageOps.Clamp((int)(paperB + (inkB - paperB) * line));
                px[i + 1] = ImageOps.Clamp((int)(paperG + (inkG - paperG) * line));
                px[i + 2] = ImageOps.Clamp((int)(paperR + (inkR - paperR) * line));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>湿壁画:石灰墙面 + 矿物颜料。</summary>
public sealed class FrescoPreset : IStylePreset
{
    public string Id => "builtin.fresco";
    public string DisplayNameKey => "Preset.Fresco.Name";
    public string DescriptionKey => "Preset.Fresco.Description";
    public string? IconGlyph => "\uE80F";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("texture", "Param.Generic.Texture", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("fade", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new FrescoStage() };
}

public sealed class FrescoStage : StageBase
{
    public override string Name => "Fresco";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double texture = Dbl(context.Parameters, "texture", 0.6);
        double fade = Dbl(context.Parameters, "fade", 0.5);

        // plaster grain: smooth random field
        var grain = new byte[w * h];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
                grain[y * w + x] = (byte)(200 + ImageOps.Hash(x / 3, y / 3, 79) * 55);
        ImageOps.BoxBlurGray(grain, w, h, 1);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                {
                    // chalky mineral pigment: desaturate toward its own luma + lift
                    int l = ImageOps.Luma(px, i);
                    double v = (px[i + c] * 0.62 + l * 0.38) * (1 - fade * 0.10) + 26 * fade;
                    v *= grain[y * w + x] / 220.0 * (1 - texture * 0.22) + texture * 0.22;
                    px[i + c] = ImageOps.Clamp((int)v);
                }
                // warm lime wash
                px[i] = ImageOps.Clamp(px[i] - 4);
                px[i + 1] = ImageOps.Clamp(px[i + 1] + 2);
                px[i + 2] = ImageOps.Clamp(px[i + 2] + 8);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>玻璃镶嵌:彩色玻璃碎片拼成图像。</summary>
public sealed class MosaicGlassPreset : IStylePreset
{
    public string Id => "builtin.mosaic-glass";
    public string DisplayNameKey => "Preset.MosaicGlass.Name";
    public string DescriptionKey => "Preset.MosaicGlass.Description";
    public string? IconGlyph => "\uEA3A";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 16, 8, 40, 2),
        new PresetParameter("sparkle", "Param.Generic.Glow", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new MosaicGlassStage() };
}

public sealed class MosaicGlassStage : StageBase
{
    public override string Name => "MosaicGlass";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 16), 6, 48);
        double sparkle = Dbl(context.Parameters, "sparkle", 0.5);

        var smooth = ImageOps.BoxBlurColor(px, stride, w, h, Math.Clamp(cell / 4, 1, 6));

        int cols = (w + cell - 1) / cell + 1, rows = (h + cell - 1) / cell + 1;
        var owner = new int[w * h];
        var seeds = new (double X, double Y)[cols, rows];
        for (int gy = 0; gy < rows; gy++)
            for (int gx = 0; gx < cols; gx++)
            {
                // irregular shards: strong jitter
                double jx = (ImageOps.Hash(gx, gy, 81) - 0.5) * cell * 0.95;
                double jy = (ImageOps.Hash(gx, gy, 83) - 0.5) * cell * 0.95;
                seeds[gx, gy] = (gx * (double)cell - jx, gy * (double)cell - jy);
            }

        // nearest shard per pixel (3×3 lattice neighborhood)
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int gx = x / cell, gy = y / cell;
                double best = double.MaxValue; int bestId = 0;
                for (int ddy = -1; ddy <= 1; ddy++)
                    for (int ddx = -1; ddx <= 1; ddx++)
                    {
                        int cx2 = gx + ddx, cy2 = gy + ddy;
                        if (cx2 < 0 || cy2 < 0 || cx2 >= cols || cy2 >= rows) continue;
                        var s = seeds[cx2, cy2];
                        double d = (s.X - x) * (s.X - x) + (s.Y - y) * (s.Y - y);
                        int id = cy2 * cols + cx2;
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
                int id = owner[y * w + x];

                bool isGrout = (x > 0 && owner[y * w + x - 1] != id) || (y > 0 && owner[(y - 1) * w + x] != id);
                if (isGrout)
                {
                    px[i] = 30; px[i + 1] = 28; px[i + 2] = 27;
                    continue;
                }

                int f = y * stride + x * 4;
                double b = smooth[f] * 1.15, g = smooth[f + 1] * 1.15, r = smooth[f + 2] * 1.15;

                // glass sparkle: linear brightness tilt across each shard
                int gx = id % cols, gy = id / cols;
                var s = seeds[gx, gy];
                double dir = ImageOps.Hash(gx, gy, 85) * Math.PI * 2;
                double off = (x - s.X) * Math.Cos(dir) + (y - s.Y) * Math.Sin(dir);
                double tilt = off / (double)cell * 0.55 * sparkle;
                b *= 1 + tilt; g *= 1 + tilt; r *= 1 + tilt;

                px[i] = ImageOps.Clamp((int)b);
                px[i + 1] = ImageOps.Clamp((int)g);
                px[i + 2] = ImageOps.Clamp((int)r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>金属雕刻:金属表面蚀刻线 + 精密机械感。</summary>
public sealed class MetalEngravingPreset : IStylePreset
{
    public string Id => "builtin.metal-engraving";
    public string DisplayNameKey => "Preset.MetalEngraving.Name";
    public string DescriptionKey => "Preset.MetalEngraving.Description";
    public string? IconGlyph => "\uE950";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("density", "Param.Generic.Density", 0.65, 0.2, 1.0, 0.05),
        new PresetParameter("gloss", "Param.Generic.Glow", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new MetalEngravingStage() };
}

public sealed class MetalEngravingStage : StageBase
{
    public override string Name => "MetalEngraving";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double density = Dbl(context.Parameters, "density", 0.65);
        double gloss = Dbl(context.Parameters, "gloss", 0.5);

        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);
        double spacing = Math.Max(2.0, 6.0 - density * 4.0);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                double t = blurred[y * w + x] / 255.0;

                // brushed steel base — dominant, with only a whisper of the original hue
                double brush = 0.92 + 0.16 * ImageOps.Hash(x, y / 2, 87);
                int b = (int)(46 * brush), g = (int)(52 * brush), r = (int)(58 * brush);

                // engraved bright lines: dual diagonal passes, width from brightness
                double cut = spacing * (0.25 + t * 0.75);
                double p1 = (x + y) % (spacing * 2);
                double p2 = (x - y + 8 * w) % (spacing * 2);
                double line = Math.Max(p1 < cut ? 1 - p1 / Math.Max(1, cut) : 0,
                                       p2 < cut * 0.6 ? 1 - p2 / Math.Max(1, cut * 0.6) : 0);
                double sheen = line * (0.55 + gloss * 0.85);

                double hb = px[i] / 255.0, hg = px[i + 1] / 255.0, hr = px[i + 2] / 255.0;
                b = (int)((b + hb * 18) * (1 + sheen * 2.1));
                g = (int)((g + hg * 20) * (1 + sheen * 2.2));
                r = (int)((r + hr * 24) * (1 + sheen * 2.3));

                px[i] = ImageOps.Clamp(b); px[i + 1] = ImageOps.Clamp(g); px[i + 2] = ImageOps.Clamp(r);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>抖动图:有限色块 + 像素抖动表现灰度。</summary>
public sealed class DitheredPreset : IStylePreset
{
    public string Id => "builtin.dithered";
    public string DisplayNameKey => "Preset.Dithered.Name";
    public string DescriptionKey => "Preset.Dithered.Description";
    public string? IconGlyph => "\uE78B";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("colors", "Param.Generic.Colors", 3, 2, 6, 1),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.4, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new DitheredStage() };
}

public sealed class DitheredStage : StageBase
{
    public override string Name => "Dithered";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int colors = Math.Clamp(Int(context.Parameters, "colors", 3), 2, 8);
        double contrast = Dbl(context.Parameters, "contrast", 0.4);

        var lut = ImageOps.ContrastLut(contrast);
        double step = 255.0 / (colors - 1);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                double threshold = (ImageOps.Bayer4(x, y) - 0.5) * step;
                for (int c = 0; c < 3; c++)
                {
                    double v = lut[px[i + c]] + threshold;
                    px[i + c] = (byte)(Math.Clamp((int)Math.Round(v / step), 0, colors - 1) * step);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>浮雕:立体凹凸起伏的表面。</summary>
public sealed class GlobeReliefPreset : IStylePreset
{
    public string Id => "builtin.globe-relief";
    public string DisplayNameKey => "Preset.GlobeRelief.Name";
    public string DescriptionKey => "Preset.GlobeRelief.Description";
    public string? IconGlyph => "\uE718";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("depth", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("colorKeep", "Param.Generic.Intensity", 0.35, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new GlobeReliefStage() };
}

public sealed class GlobeReliefStage : StageBase
{
    public override string Name => "GlobeRelief";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double depth = Dbl(context.Parameters, "depth", 0.6);
        double colorKeep = Dbl(context.Parameters, "colorKeep", 0.35);

        var luma = ImageOps.LumaMap(src);
        var original = (byte[])px.Clone();

        Parallel.For(1, h - 1, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 1; x < w - 1; x++)
            {
                int i = y * stride + x * 4;
                double emboss = (luma[(y - 1) * w + x - 1] - luma[(y + 1) * w + x + 1]) / 255.0;
                double relief = 0.5 + emboss * (0.5 + depth * 2.2);

                int pb = original[i], pg = original[i + 1], pr = original[i + 2];
                px[i] = ImageOps.Clamp((int)((128 * (1 - colorKeep) + pb * colorKeep) * relief + (1 - relief) * 40));
                px[i + 1] = ImageOps.Clamp((int)((128 * (1 - colorKeep) + pg * colorKeep) * relief + (1 - relief) * 40));
                px[i + 2] = ImageOps.Clamp((int)((128 * (1 - colorKeep) + pr * colorKeep) * relief + (1 - relief) * 40));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}
