using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Styles 37-48: light, liquid and fiber styles ============

/// <summary>霓虹灯牌:发光管轮廓 + 夜间商业街。</summary>
public sealed class NeonSignPreset : IStylePreset
{
    public string Id => "builtin.neon-sign";
    public string DisplayNameKey => "Preset.NeonSign.Name";
    public string DescriptionKey => "Preset.NeonSign.Description";
    public string? IconGlyph => "\uE793";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("glow", "Param.Generic.Glow", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("spread", "Param.Generic.Density", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new NeonSignStage() };
}

public sealed class NeonSignStage : StageBase
{
    public override string Name => "NeonSign";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double glow = Dbl(context.Parameters, "glow", 0.6);
        double spread = Dbl(context.Parameters, "spread", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = 130f;

        // tube map: edges pick a hue from gradient orientation
        var tubes = new float[w * h * 3];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = Math.Clamp((mag[y * w + x] - threshold) / 260.0, 0, 1);
                if (t <= 0) continue;
                int xp = Math.Min(w - 1, x + 1), yp = Math.Min(h - 1, y + 1);
                double angle = Math.Atan2(luma[yp * w + x] - luma[y * w + x], luma[y * w + xp] - luma[y * w + x]);
                double hue = (angle / (2 * Math.PI) + 0.62) % 1.0;
                var (r, g, b) = NeonCyberpunkStage.HsvToRgb(hue, 0.9, 1.0);
                tubes[y * w * 3 + x * 3] = (float)(b * t);
                tubes[y * w * 3 + x * 3 + 1] = (float)(g * t);
                tubes[y * w * 3 + x * 3 + 2] = (float)(r * t);
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.5 * (y + 1) / (double)h));
        });

        ImageOps.BoxBlurRgb(tubes, w, h, Math.Max(1, (int)(2 + spread * 6)));

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int ni = y * w * 3 + x * 3;

                    // night wall, barely showing the scene
                    double wall = luma[y * w + x] / 255.0 * 0.12;
                    double b = 10 + wall * 30, g = 11 + wall * 32, r = 13 + wall * 36;

                    for (int c = 0; c < 3; c++)
                    {
                        double tubeV = tubes[ni + c];
                        double core = Math.Min(1.0, tubeV * 1.6) * glow;       // hot tube core
                        double halo = Math.Min(1.0, tubeV) * glow * 0.85;      // wide halo
                        double v = (c == 0 ? b : c == 1 ? g : r) / 255.0;
                        double lit = 1.0 - (1.0 - Math.Max(v, halo)) * (1.0 - core * 0.9);
                        double outV = Math.Max(lit, halo * 0.75);
                        if (c == 0) b = outV * 255; else if (c == 1) g = outV * 255; else r = outV * 255;
                    }

                    px[i] = ImageOps.Clamp((int)b);
                    px[i + 1] = ImageOps.Clamp((int)g);
                    px[i + 2] = ImageOps.Clamp((int)r);
                }
            }
        }, ct);

        return context;
    }
}

/// <summary>液态金属:镜面金属 + 流动反射。</summary>
public sealed class LiquidMetalPreset : IStylePreset
{
    public string Id => "builtin.liquid-metal";
    public string DisplayNameKey => "Preset.LiquidMetal.Name";
    public string DescriptionKey => "Preset.LiquidMetal.Description";
    public string? IconGlyph => "\uE950";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("flow", "Param.Generic.Texture", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new LiquidMetalStage() };
}

public sealed class LiquidMetalStage : StageBase
{
    public override string Name => "LiquidMetal";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double flow = Dbl(context.Parameters, "flow", 0.6);
        double contrast = Dbl(context.Parameters, "contrast", 0.6);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                // molten displacement: layered sine curls
                double ph = Math.Sin(y * 0.032) + Math.Sin(x * 0.021 + 1.7);
                double sx = x + Math.Sin(y * 0.05 + ph * 2.2) * 9 * flow;
                double sy = y + Math.Sin(x * 0.043 + ph * 1.6) * 9 * flow;
                int nx = Math.Clamp((int)sx, 0, w - 1), ny = Math.Clamp((int)sy, 0, h - 1);

                double t = lut[luma[ny * w + nx]] / 255.0;
                // chrome melt ramp: deep navy → cool silver → white hot, with band ripple
                double band = 0.88 + 0.12 * Math.Sin(y * 0.09 + Math.Sin(x * 0.015) * 2.0);
                double v = Math.Pow(t, 1.3) * band;
                double r = v < 0.72 ? 26 + v * 240 : 232 + (v - 0.72) * 80;
                double g = v < 0.72 ? 34 + v * 262 : 236 + (v - 0.72) * 72;
                double b = v < 0.72 ? 58 + v * 288 : 244 + (v - 0.72) * 55;

                int i2 = y * stride + x * 4;
                px[i2] = ImageOps.Clamp((int)r);
                px[i2 + 1] = ImageOps.Clamp((int)g);
                px[i2 + 2] = ImageOps.Clamp((int)b);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>镀铬:高反射银色表面。</summary>
public sealed class ChromePreset : IStylePreset
{
    public string Id => "builtin.chrome";
    public string DisplayNameKey => "Preset.Chrome.Name";
    public string DescriptionKey => "Preset.Chrome.Description";
    public string? IconGlyph => "\uE950";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("offset", "Param.Generic.Offset", 0.5, 0.1, 0.9, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ChromeStage() };
}

public sealed class ChromeStage : StageBase
{
    public override string Name => "Chrome";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.6);
        double horizon = Dbl(context.Parameters, "offset", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 2);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            // per-row vertical position shapes the sky/ground reflection
            double vy = Math.Clamp((y / (double)h - (1 - horizon)) / horizon, 0, 1);
            for (int x = 0; x < w; x++)
            {
                double t = lut[blurred[y * w + x]] / 255.0;
                // reflection profile: bright bands flip at the horizon line
                double band = t * (1 - Math.Abs(vy - 0.5)) + vy * 0.35;
                band = Math.Clamp(band, 0, 1);
                double mirror = band > 0.5 ? 1 - band : band;          // fold into 0..0.5
                double sky = 1 - mirror * 1.6;                          // bright sky metal
                double ground = 0.15 + mirror * 0.9;                    // dark ground metal

                double k = vy;                                          // 0 = top (sky) → 1 = bottom
                double v = sky * (1 - k) + ground * k;

                int i = y * stride + x * 4;
                // cold chrome tint
                px[i] = ImageOps.Clamp((int)(v * 235));
                px[i + 1] = ImageOps.Clamp((int)(v * 242));
                px[i + 2] = ImageOps.Clamp((int)(v * 252));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>发光线框:发光网格和轮廓线构成物体。</summary>
public sealed class GlowingWireframePreset : IStylePreset
{
    public string Id => "builtin.glowing-wireframe";
    public string DisplayNameKey => "Preset.GlowingWireframe.Name";
    public string DescriptionKey => "Preset.GlowingWireframe.Description";
    public string? IconGlyph => "\uF0B2";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("density", "Param.Generic.Density", 0.5, 0.1, 1.0, 0.05),
        new PresetParameter("glow", "Param.Generic.Glow", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new GlowingWireframeStage() };
}

public sealed class GlowingWireframeStage : StageBase
{
    public override string Name => "GlowingWireframe";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double density = Dbl(context.Parameters, "density", 0.5);
        double glow = Dbl(context.Parameters, "glow", 0.6);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = 150f;

        var glowMap = new float[w * h * 3];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double edge = Math.Clamp((mag[y * w + x] - threshold) / 300.0, 0, 1);
                double grid = (x % Math.Max(6, (int)(30 - density * 22)) == 0) || (y % Math.Max(6, (int)(30 - density * 22)) == 0)
                    ? 0.12 + edge * 0.35 : 0.0;
                double v = Math.Clamp(edge + grid, 0, 1);

                // channels stored BGRA-order to match the blend loop below: blue, green, red
                glowMap[y * w * 3 + x * 3] = (float)(v);
                glowMap[y * w * 3 + x * 3 + 1] = (float)(v * 0.95);
                glowMap[y * w * 3 + x * 3 + 2] = (float)(v * (0.35 + edge * 0.3));
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.5 * (y + 1) / (double)h));
        });

        ImageOps.BoxBlurRgb(glowMap, w, h, 2);

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int gi = y * w * 3 + x * 3;
                    double b = 8, g = 10, r = 12;
                    for (int c = 0; c < 3; c++)
                    {
                        double baseV = c == 0 ? b : c == 1 ? g : r;
                        double screen = 1.0 - (1.0 - baseV / 255.0) * (1.0 - Math.Min(1.0, glowMap[gi + c]) * glow);
                        if (c == 0) b = screen * 255; else if (c == 1) g = screen * 255; else r = screen * 255;
                    }
                    px[i] = ImageOps.Clamp((int)b);
                    px[i + 1] = ImageOps.Clamp((int)g);
                    px[i + 2] = ImageOps.Clamp((int)r);
                }
            }
        }, ct);

        return context;
    }
}

/// <summary>撕纸:不规则纸边 + 层叠遮挡。</summary>
public sealed class TornPaperPreset : IStylePreset
{
    public string Id => "builtin.torn-paper";
    public string DisplayNameKey => "Preset.TornPaper.Name";
    public string DescriptionKey => "Preset.TornPaper.Description";
    public string? IconGlyph => "\uE8B2";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("strips", "Param.Generic.Density", 4, 2, 7, 1),
        new PresetParameter("offset", "Param.Generic.Offset", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new TornPaperStage() };
}

public sealed class TornPaperStage : StageBase
{
    public override string Name => "TornPaper";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int strips = Math.Clamp(Int(context.Parameters, "strips", 4), 2, 8);
        double offset = Dbl(context.Parameters, "offset", 0.5);

        var original = (byte[])px.Clone();
        var dst = new RgbaImageBuffer(w, h);
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        double stripH = h / (double)strips;

        for (int s = strips - 1; s >= 0; s--) // bottom strips first, top strips overlap
        {
            double shiftX = (ImageOps.Hash(s, 1, 91) - 0.5) * w * 0.10 * offset;
            double shiftY = (ImageOps.Hash(s, 2, 93) - 0.5) * stripH * 0.3 * offset;

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double sy = (y - shiftY) / stripH;
                    if (sy < s || sy >= s + 1) continue;

                    // torn edge: irregular boundary between strips
                    double local = sy - s;
                    double tearNoise = (ImageOps.Hash(x / 4, s, 95) - 0.5) * 0.06;
                    if (local > 1 + tearNoise) continue;
                    bool tornEdge = local > 0.94 + tearNoise || local < 0.05 + tearNoise;

                    int sx = Math.Clamp((int)(x - shiftX), 0, w - 1);
                    int syy = Math.Clamp(y, 0, h - 1);
                    int oi = y * outStride + x * 4;
                    int ii = syy * stride + sx * 4;

                    if (tornEdge)
                    {
                        outPx[oi] = 242; outPx[oi + 1] = 239; outPx[oi + 2] = 231;
                    }
                    else
                    {
                        outPx[oi] = original[ii]; outPx[oi + 1] = original[ii + 1]; outPx[oi + 2] = original[ii + 2];
                        // soft shadow cast on the strip below
                        if (local < 0.10)
                        {
                            outPx[oi] = ImageOps.Clamp(outPx[oi] - 22);
                            outPx[oi + 1] = ImageOps.Clamp(outPx[oi + 1] - 22);
                            outPx[oi + 2] = ImageOps.Clamp(outPx[oi + 2] - 22);
                        }
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (strips - s) / (double)strips));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>胶带艺术:彩色胶带切割拼贴。</summary>
public sealed class TapeArtPreset : IStylePreset
{
    public string Id => "builtin.tape-art";
    public string DisplayNameKey => "Preset.TapeArt.Name";
    public string DescriptionKey => "Preset.TapeArt.Description";
    public string? IconGlyph => "\uE8E8";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("bands", "Param.Generic.Density", 10, 4, 24, 1),
        new PresetParameter("colors", "Param.Generic.Colors", 6, 3, 12, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new TapeArtStage() };
}

public sealed class TapeArtStage : StageBase
{
    public override string Name => "TapeArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int bands = Math.Clamp(Int(context.Parameters, "bands", 10), 3, 32);
        int colors = Math.Clamp(Int(context.Parameters, "colors", 6), 2, 16);

        var palette = ImageOps.ExtractPalette(src, colors);
        var original = (byte[])px.Clone();
        double bandH = h / (double)bands;

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            int band = (int)(y / bandH);
            double local = y / bandH - band;

            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;

                // strip edges: lighter adhesive sheen + tiny dark gap
                double edge = Math.Min(local, 1 - local);
                int ii = y * stride + Math.Clamp(x, 0, w - 1) * 4;
                var p = palette[ImageOps.NearestPaletteIndex(palette, original[ii], original[ii + 1], original[ii + 2])];

                double sheen = edge < 0.12 ? 0.35 : edge > 0.85 ? 0.18 : 0.0;
                double streak = 1.0 + (ImageOps.Hash(x / 2, band, 97) - 0.5) * 0.10;
                // tape strips shear slightly against each other
                int sx = Math.Clamp(x + (int)((band & 1) == 0 ? 3 : -3), 0, w - 1);
                int ii2 = y * stride + sx * 4;
                p = palette[ImageOps.NearestPaletteIndex(palette, original[ii2], original[ii2 + 1], original[ii2 + 2])];

                int b = ImageOps.Clamp((int)(p.B * streak + 60 * sheen));
                int g = ImageOps.Clamp((int)(p.G * streak + 60 * sheen));
                int r = ImageOps.Clamp((int)(p.R * streak + 55 * sheen));
                if (edge < 0.04) { b = ImageOps.Clamp(b - 60); g = ImageOps.Clamp(g - 60); r = ImageOps.Clamp(r - 60); }

                px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>绳线艺术:大量交叉线形成轮廓。</summary>
public sealed class StringArtPreset : IStylePreset
{
    public string Id => "builtin.string-art";
    public string DisplayNameKey => "Preset.StringArt.Name";
    public string DescriptionKey => "Preset.StringArt.Description";
    public string? IconGlyph => "\uE8E7";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("lines", "Param.Generic.Density", 0.6, 0.1, 1.0, 0.05),
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new StringArtStage() };
}

public sealed class StringArtStage : StageBase
{
    public override string Name => "StringArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double lineFrac = Dbl(context.Parameters, "lines", 0.6);
        double detail = Dbl(context.Parameters, "detail", 0.5);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = (1f - (float)detail) * 240f + 50f;

        // collect anchor points on strong edges
        var anchors = new List<(int X, int Y, float M)>();
        for (int y = 1; y < h - 1; y += 2)
            for (int x = 1; x < w - 1; x += 2)
                if (mag[y * w + x] > threshold)
                    anchors.Add((x, y, mag[y * w + x]));
        if (anchors.Count < 2)
        {
            progress?.Report(new StageProgress(Name, 0, 1, 1.0));
            return Task.FromResult(context);
        }

        var acc = new float[w * h];
        int lineCount = Math.Clamp((int)(anchors.Count * 1.4 * lineFrac), 200, 12000);

        for (int li = 0; li < lineCount; li++)
        {
            // deterministic weighted picks: brighter edges host more threads
            var a = anchors[(int)(ImageOps.Hash(li, 1, 99) * (anchors.Count - 1))];
            var b = anchors[(int)(ImageOps.Hash(li, 2, 101) * (anchors.Count - 1))];
            if (a.X == b.X && a.Y == b.Y) continue;

            double steps = Math.Max(Math.Abs(b.X - a.X), Math.Abs(b.Y - a.Y));
            double weight = 0.16 + (a.M + b.M) / 2040.0;
            for (int s = 0; s <= steps; s++)
            {
                double t = s / steps;
                int px2 = (int)Math.Round(a.X + (b.X - a.X) * t);
                int py2 = (int)Math.Round(a.Y + (b.Y - a.Y) * t);
                acc[py2 * w + px2] += (float)weight;
            }
            if ((li & 255) == 0) progress?.Report(new StageProgress(Name, 0, 1, 0.85 * li / (double)lineCount));
        }

        // render threads over dark board
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                double v = Math.Clamp(acc[y * w + x] / 6.0, 0, 1);
                px[i] = ImageOps.Clamp((int)(24 + v * 216));
                px[i + 1] = ImageOps.Clamp((int)(22 + v * 210));
                px[i + 2] = ImageOps.Clamp((int)(26 + v * 200));
            }
        }
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}

/// <summary>沙画:沙粒堆积 + 流动纹理。</summary>
public sealed class SandArtPreset : IStylePreset
{
    public string Id => "builtin.sand-art";
    public string DisplayNameKey => "Preset.SandArt.Name";
    public string DescriptionKey => "Preset.SandArt.Description";
    public string? IconGlyph => "\uE81C";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("grain", "Param.Generic.Grain", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("spread", "Param.Generic.Texture", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new SandArtStage() };
}

public sealed class SandArtStage : StageBase
{
    public override string Name => "SandArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double grain = Dbl(context.Parameters, "grain", 0.6);
        double spread = Dbl(context.Parameters, "spread", 0.5);

        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1 + (int)(spread * 4));

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // backlit glass: near-black warm base
                int b = 14, g = 13, r = 16;

                double t = blurred[y * w + x] / 255.0;
                double drift = Math.Sin(x * 0.02 + t * 6) * spread * 3;
                int sx = Math.Clamp((int)(x - drift), 0, w - 1);
                t = blurred[y * w + sx] / 255.0;

                if (t > 0.30)
                {
                    // sand piles: warm grains whose density follows brightness
                    double coverage = Math.Clamp((t - 0.30) / 0.55, 0, 1);
                    double spark = ImageOps.Hash(x, y, 103);
                    double k = coverage * (0.55 + 0.45 * spark) * (1 - grain * 0.35 * (spark > 0.75 ? 1 : 0));
                    b = ImageOps.Clamp((int)(b + (150 - b) * k));
                    g = ImageOps.Clamp((int)(g + (190 - g) * k));
                    r = ImageOps.Clamp((int)(r + (226 - r) * k));
                }

                px[i] = (byte)b; px[i + 1] = (byte)g; px[i + 2] = (byte)r;
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>烟雾:烟雾轮廓 + 柔软扩散。</summary>
public sealed class SmokeArtPreset : IStylePreset
{
    public string Id => "builtin.smoke-art";
    public string DisplayNameKey => "Preset.SmokeArt.Name";
    public string DescriptionKey => "Preset.SmokeArt.Description";
    public string? IconGlyph => "\uE80F";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("drift", "Param.Generic.Texture", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("softness", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new SmokeArtStage() };
}

public sealed class SmokeArtStage : StageBase
{
    public override string Name => "SmokeArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double drift = Dbl(context.Parameters, "drift", 0.6);
        double softness = Dbl(context.Parameters, "softness", 0.6);

        var luma = ImageOps.LumaMap(src);
        var smoke = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(smoke, w, h, 3 + (int)(softness * 9));
        ImageOps.BoxBlurGray(smoke, w, h, Math.Max(1, (3 + (int)(softness * 9)) / 2));

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                // wispy displacement: two crossed sine fields
                double dx2 = Math.Sin(y * 0.028 + x * 0.011) * 10 * drift;
                double dy2 = Math.Cos(x * 0.024 + y * 0.013) * 8 * drift;
                int sx = Math.Clamp((int)(x + dx2), 0, w - 1);
                int sy = Math.Clamp((int)(y + dy2), 0, h - 1);

                double v = 1.0 - smoke[sy * w + sx] / 255.0; // dark subjects become bright smoke
                v = Math.Pow(v, 1.2);

                int i = y * stride + x * 4;
                px[i] = ImageOps.Clamp((int)(12 + v * 150));
                px[i + 1] = ImageOps.Clamp((int)(14 + v * 165));
                px[i + 2] = ImageOps.Clamp((int)(18 + v * 185));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>光绘:长曝光光轨。</summary>
public sealed class LightPaintingPreset : IStylePreset
{
    public string Id => "builtin.light-painting";
    public string DisplayNameKey => "Preset.LightPainting.Name";
    public string DescriptionKey => "Preset.LightPainting.Description";
    public string? IconGlyph => "\uE734";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("streak", "Param.Generic.Texture", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("glow", "Param.Generic.Glow", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new LightPaintingStage() };
}

public sealed class LightPaintingStage : StageBase
{
    public override string Name => "LightPainting";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double streak = Dbl(context.Parameters, "streak", 0.6);
        double glow = Dbl(context.Parameters, "glow", 0.6);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);
        float threshold = 120f;

        var trails = new float[w * h * 3];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double e = Math.Clamp((mag[y * w + x] - threshold) / 320.0, 0, 1);
                if (e <= 0) continue;
                double hue = ((x / (double)w) * 0.8 + (y / (double)h) * 0.35) % 1.0;
                var (r, g, b) = NeonCyberpunkStage.HsvToRgb(hue, 0.75, 1.0);
                trails[y * w * 3 + x * 3] = (float)(b * e);
                trails[y * w * 3 + x * 3 + 1] = (float)(g * e);
                trails[y * w * 3 + x * 3 + 2] = (float)(r * e);
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.5 * (y + 1) / (double)h));
        });

        // anisotropic streak: strong horizontal + lighter vertical smears
        ImageOps.BoxBlurRgb(trails, w, h, Math.Max(1, (int)(2 + streak * 10)));
        ImageOps.BoxBlurRgb(trails, w, h, 2);

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int ti = y * w * 3 + x * 3;
                    for (int c = 0; c < 3; c++)
                    {
                        double baseV = px[i + c] / 255.0 * 0.18; // near-black exposure
                        double light = Math.Min(1.0, trails[ti + c]) * glow;
                        double screen = 1.0 - (1.0 - baseV) * (1.0 - light);
                        px[i + c] = ImageOps.Clamp((int)(screen * 255));
                    }
                }
            }
        }, ct);

        return context;
    }
}

/// <summary>万花筒:镜像对称 + 重复几何。</summary>
public sealed class KaleidoscopePreset : IStylePreset
{
    public string Id => "builtin.kaleidoscope";
    public string DisplayNameKey => "Preset.Kaleidoscope.Name";
    public string DescriptionKey => "Preset.Kaleidoscope.Description";
    public string? IconGlyph => "\uE914";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("segments", "Param.Generic.Segments", 8, 4, 16, 2),
        new PresetParameter("zoom", "Param.Generic.Size", 1.0, 0.5, 2.0, 0.1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new KaleidoscopeStage() };
}

public sealed class KaleidoscopeStage : StageBase
{
    public override string Name => "Kaleidoscope";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        int segments = Math.Clamp(Int(context.Parameters, "segments", 8), 3, 24);
        double zoom = Dbl(context.Parameters, "zoom", 1.0);

        var original = (byte[])px.Clone();
        double cx = w / 2.0, cy = h / 2.0;
        double segAngle = 2 * Math.PI / segments;
        var quad = new byte[4];

        for (int y = 0; y < h; y++)
        {
            double ry = y - cy;
            for (int x = 0; x < w; x++)
            {
                double rx = x - cx;
                double r = Math.Sqrt(rx * rx + ry * ry) / zoom;
                double angle = Math.Atan2(ry, rx);
                // fold into one segment, mirroring alternate folds; aim the wedge upward
                double a = ((angle % segAngle) + segAngle) % segAngle;
                if (a > segAngle / 2) a = segAngle - a;
                double sa = a - Math.PI / 2;

                // fold the radius too, so the frame fills with mirrored content
                double maxR = Math.Min(cx, cy) * 1.25;
                if (r > maxR) r = Math.Max(0, 2 * maxR - r);

                double sx = cx + Math.Cos(sa) * r;
                double sy = cy + Math.Sin(sa) * r;
                ImageOps.SampleBilinear(original, stride, w, h, sx, sy, quad);

                int i = y * stride + x * 4;
                px[i] = quad[0]; px[i + 1] = quad[1]; px[i + 2] = quad[2];
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        }
        return Task.FromResult(context);
    }
}

/// <summary>液体大理石:大理石流纹 + 抽象纹理。</summary>
public sealed class LiquidMarblePreset : IStylePreset
{
    public string Id => "builtin.liquid-marble";
    public string DisplayNameKey => "Preset.LiquidMarble.Name";
    public string DescriptionKey => "Preset.LiquidMarble.Description";
    public string? IconGlyph => "\uE9D9";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("flow", "Param.Generic.Texture", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("veins", "Param.Generic.Density", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new LiquidMarbleStage() };
}

public sealed class LiquidMarbleStage : StageBase
{
    public override string Name => "LiquidMarble";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double flow = Dbl(context.Parameters, "flow", 0.6);
        double veins = Dbl(context.Parameters, "veins", 0.5);

        var smooth = ImageOps.BoxBlurColor(px, stride, w, h, 4);
        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;

                // marbling swirl: crossed sine displacement of the pastel base
                double ph = Math.Sin(x * 0.017 + 0.8) + Math.Sin(y * 0.019 + 2.1);
                double sx = x + Math.Sin(y * 0.031 + ph) * 14 * flow + Math.Sin(x * 0.008) * 8 * flow;
                double sy = y + Math.Cos(x * 0.027 + ph) * 12 * flow + Math.Sin(y * 0.009) * 7 * flow;
                var quad = new byte[4];
                ImageOps.SampleBilinear(smooth, stride, w, h, sx, sy, quad);

                // stone veins: smooth flowing ridges of a folded sine field
                double field = Math.Sin(x * 0.015 + Math.Sin(y * 0.013) * 3.0 + ImageOps.Hash(x / 14, y / 14, 105) * 2.2 * flow);
                double vein = Math.Exp(-Math.Pow(field / (0.12 + (1 - veins) * 0.32), 2));

                int b = ImageOps.Clamp((int)(quad[0] * (1 - vein * 0.72)));
                int g = ImageOps.Clamp((int)(quad[1] * (1 - vein * 0.68)));
                int r = ImageOps.Clamp((int)(quad[2] * (1 - vein * 0.60)));

                // soft sheen from the untouched original
                px[i] = ImageOps.Clamp((int)(b * 0.85 + original[i] * 0.15));
                px[i + 1] = ImageOps.Clamp((int)(g * 0.85 + original[i + 1] * 0.15));
                px[i + 2] = ImageOps.Clamp((int)(r * 0.85 + original[i + 2] * 0.15));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}
