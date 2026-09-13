using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Group B: photography & signal styles ============
// Neon / Glitch / CRT / Holographic / XRay / Thermal / Infrared / DoubleExposure / FilmNegative / Polaroid.

/// <summary>霓虹赛博:霓虹光源 + 未来感。</summary>
public sealed class NeonCyberpunkPreset : IStylePreset
{
    public string Id => "builtin.neon-cyberpunk";
    public string DisplayNameKey => "Preset.NeonCyberpunk.Name";
    public string DescriptionKey => "Preset.NeonCyberpunk.Description";
    public string? IconGlyph => "\uE7F8";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("glow", "Param.Generic.Glow", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("intensity", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new NeonCyberpunkStage() };
}

public sealed class NeonCyberpunkStage : StageBase
{
    public override string Name => "NeonCyberpunk";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double glow = Dbl(context.Parameters, "glow", 0.6);
        double intensity = Dbl(context.Parameters, "intensity", 0.6);

        var luma = ImageOps.LumaMap(src);
        var mag = ImageOps.SobelMagnitude(luma, w, h);

        // neon map: bright edges pick a hue from their gradient orientation
        var neon = new float[w * h * 3];
        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = Math.Clamp(mag[y * w + x] / 380.0, 0, 1);
                if (t < 0.08) continue;
                int xm = Math.Max(0, x - 1), xp = Math.Min(w - 1, x + 1);
                int ym = Math.Max(0, y - 1), yp = Math.Min(h - 1, y + 1);
                double angle = Math.Atan2(luma[yp * w + x] - luma[ym * w + x], luma[y * w + xp] - luma[y * w + xm]);
                double hue = (angle / (2 * Math.PI) + 0.55) % 1.0; // magenta→cyan range
                var (r, g, b) = HsvToRgb(hue, 0.95, 1.0);
                neon[y * w * 3 + x * 3] = (float)(b * t);
                neon[y * w * 3 + x * 3 + 1] = (float)(g * t);
                neon[y * w * 3 + x * 3 + 2] = (float)(r * t);
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.5 * (y + 1) / (double)h));
        });

        ImageOps.BoxBlurRgb(neon, w, h, Math.Max(1, (int)(2 + glow * 5)));

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    int i = y * stride + x * 4;
                    int ni = y * w * 3 + x * 3;

                    // crush shadows toward navy, push saturation
                    for (int c = 0; c < 3; c++)
                    {
                        double v = px[i + c] / 255.0;
                        v = Math.Pow(v, 1.0 + intensity * 0.6);           // deepen shadows
                        v = Math.Pow(v, 1.0 / (1.0 + intensity * 0.35));  // lift mids
                        px[i + c] = ImageOps.Clamp((int)(v * 255));
                    }

                    // additive neon glow
                    px[i] = ImageOps.Clamp(px[i] + (int)(neon[ni] * 255 * glow));
                    px[i + 1] = ImageOps.Clamp(px[i + 1] + (int)(neon[ni + 1] * 255 * glow));
                    px[i + 2] = ImageOps.Clamp(px[i + 2] + (int)(neon[ni + 2] * 255 * glow));
                }
            }
        }, ct);

        return context;
    }

    internal static (byte R, byte G, byte B) HsvToRgb(double h, double s, double v)
    {
        int i = (int)(h * 6) % 6;
        double f = h * 6 - Math.Floor(h * 6);
        double p = v * (1 - s), q = v * (1 - f * s), t = v * (1 - (1 - f) * s);
        return i switch
        {
            0 => (To(v), To(t), To(p)),
            1 => (To(q), To(v), To(p)),
            2 => (To(p), To(v), To(t)),
            3 => (To(p), To(q), To(v)),
            4 => (To(t), To(p), To(v)),
            _ => (To(v), To(p), To(q)),
        };
        static byte To(double d) => (byte)Math.Clamp((int)(d * 255), 0, 255);
    }
}

/// <summary>故障艺术:RGB 分离 + 数字撕裂。</summary>
public sealed class GlitchPreset : IStylePreset
{
    public string Id => "builtin.glitch";
    public string DisplayNameKey => "Preset.Glitch.Name";
    public string DescriptionKey => "Preset.Glitch.Description";
    public string? IconGlyph => "\uE946";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("intensity", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("slices", "Param.Generic.Density", 12, 2, 48, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new GlitchStage() };
}

public sealed class GlitchStage : StageBase
{
    public override string Name => "Glitch";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double intensity = Dbl(context.Parameters, "intensity", 0.5);
        int slices = Math.Clamp(Int(context.Parameters, "slices", 12), 2, 64);

        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            // per-row deterministic slice displacement
            double band = ImageOps.Hash(0, y / Math.Max(1, h / slices), 21);
            int shift = (int)((band - 0.5) * intensity * w * 0.18);

            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                int xr = Math.Clamp(x + shift, 0, w - 1);
                int xb = Math.Clamp(x - shift, 0, w - 1);

                int ir = y * stride + xr * 4;
                int ib = y * stride + xb * 4;

                px[i] = original[ib];                       // B channel shifted left
                px[i + 1] = original[i + 1];
                px[i + 2] = original[ir + 2];               // R channel shifted right
            }

            // occasional torn white/colored band
            if (ImageOps.Hash(0, y, 33) > 0.985 - intensity * 0.03)
            {
                int startX = (int)(ImageOps.Hash(1, y, 5) * w * 0.5);
                int len = (int)(w * (0.05 + ImageOps.Hash(2, y, 9) * 0.3));
                for (int x = startX; x < Math.Min(w, startX + len); x++)
                {
                    int i = y * stride + x * 4;
                    byte v = (byte)(200 + ImageOps.Hash(3, x, y) * 55);
                    px[i] = v; px[i + 1] = v; px[i + 2] = (byte)(255 - v);
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>CRT 复古:扫描线 + 荧光辉光 + 屏幕弯曲。</summary>
public sealed class CRTPreset : IStylePreset
{
    public string Id => "builtin.crt";
    public string DisplayNameKey => "Preset.CRT.Name";
    public string DescriptionKey => "Preset.CRT.Description";
    public string? IconGlyph => "\uE7F8";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("scanline", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("curvature", "Param.Generic.Intensity", 0.3, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new CRTStage() };
}

public sealed class CRTStage : StageBase
{
    public override string Name => "CRT";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double scanline = Dbl(context.Parameters, "scanline", 0.6);
        double curvature = Dbl(context.Parameters, "curvature", 0.3);

        var original = (byte[])px.Clone();
        var originalBuf = new RgbaImageBuffer(w, h, original);
        var glow = new float[w * h * 3];
        for (int i = 0, o = 0; i < w * h; i++, o += 4)
        {
            glow[i * 3] = original[o] / 255f;
            glow[i * 3 + 1] = original[o + 1] / 255f;
            glow[i * 3 + 2] = original[o + 2] / 255f;
        }
        ImageOps.BoxBlurRgb(glow, w, h, 2);

        var tmp = new byte[px.Length];
        var tmpBuf = new RgbaImageBuffer(w, h, tmp);

        // barrel distortion remap into tmp
        double cx = w / 2.0, cy = h / 2.0, k = curvature * 0.12;
        var quad = new byte[4];
        for (int y = 0; y < h; y++)
        {
            double ny = (y - cy) / cy;
            for (int x = 0; x < w; x++)
            {
                double nx = (x - cx) / cx;
                double r2 = nx * nx + ny * ny;
                double sx = cx + nx * cx * (1 + k * r2);
                double sy = cy + ny * cy * (1 + k * r2);
                ImageOps.SampleBilinear(original, stride, w, h, sx, sy, quad);
                int o = y * stride + x * 4;
                tmp[o] = quad[0]; tmp[o + 1] = quad[1]; tmp[o + 2] = quad[2]; tmp[o + 3] = 255;
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.6 * (y + 1) / (double)h));
        }

        // scanlines + phosphor glow + green-magenta tube tint
        for (int y = 0; y < h; y++)
        {
            double scan = 1.0 - scanline * (0.35 + 0.25 * Math.Sin(y * Math.PI / 1.5));
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                int gi = y * w * 3 + x * 3;
                for (int c = 0; c < 3; c++)
                {
                    double v = tmp[i + c] * scan + glow[gi + c] * 255 * 0.22;
                    tmp[i + c] = ImageOps.Clamp((int)v);
                }
                // subtle tube tint: lift green mid slightly
                tmp[i + 1] = ImageOps.Clamp(tmp[i + 1] + 4);
            }
        }

        Buffer.BlockCopy(tmp, 0, px, 0, px.Length);
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}

/// <summary>全息:彩虹折射 + 金属光泽。</summary>
public sealed class HolographicPreset : IStylePreset
{
    public string Id => "builtin.holographic";
    public string DisplayNameKey => "Preset.Holographic.Name";
    public string DescriptionKey => "Preset.Holographic.Description";
    public string? IconGlyph => "\uE9F3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("intensity", "Param.Generic.Intensity", 0.7, 0.0, 1.0, 0.05),
        new PresetParameter("bands", "Param.Generic.Density", 6, 1, 20, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new HolographicStage() };
}

public sealed class HolographicStage : StageBase
{
    public override string Name => "Holographic";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double intensity = Dbl(context.Parameters, "intensity", 0.7);
        double bands = Dbl(context.Parameters, "bands", 6);

        var luma = ImageOps.LumaMap(src);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = luma[y * w + x] / 255.0;
                double hue = (t * 0.6 + 0.55 + 0.12 * Math.Sin(x * 0.05 * bands + y * 0.03 * bands)) % 1.0;
                double sheen = 0.72 + 0.28 * Math.Sin((x * 0.7 + y * 0.7) * 0.05 * bands + t * 5);
                var (r, g, b) = NeonCyberpunkStage.HsvToRgb(hue, 0.65 * sheen, Math.Clamp(0.55 + t * 0.45, 0, 1));

                int i = y * stride + x * 4;
                double mix = intensity * 0.75;
                px[i] = ImageOps.Clamp((int)(px[i] * (1 - mix) + b * mix));
                px[i + 1] = ImageOps.Clamp((int)(px[i + 1] * (1 - mix) + g * mix));
                px[i + 2] = ImageOps.Clamp((int)(px[i + 2] * (1 - mix) + r * mix));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>X 光:冷色医学成像 + 内部轮廓。</summary>
public sealed class XRayPreset : IStylePreset
{
    public string Id => "builtin.xray";
    public string DisplayNameKey => "Preset.XRay.Name";
    public string DescriptionKey => "Preset.XRay.Description";
    public string? IconGlyph => "\uE9D5";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("tint", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new XRayStage() };
}

public sealed class XRayStage : StageBase
{
    public override string Name => "XRay";
    public override async Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.6);
        double tint = Dbl(context.Parameters, "tint", 0.6);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);

        // glow layer from inverted luma
        var glow = new float[w * h * 3];
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                float v = (255 - luma[y * w + x]) / 255f;
                glow[y * w * 3 + x * 3] = v; glow[y * w * 3 + x * 3 + 1] = v; glow[y * w * 3 + x * 3 + 2] = v;
            }
        ImageOps.BoxBlurRgb(glow, w, h, 3);

        await Task.Run(() =>
        {
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    double inv = lut[255 - luma[y * w + x]] / 255.0; // bones bright
                    double gi = glow[y * w * 3 + x * 3];
                    double v = Math.Clamp(inv * 0.85 + gi * 0.35, 0, 1);

                    int i = y * stride + x * 4;
                    px[i] = ImageOps.Clamp((int)((v * 255) * (1 - tint * 0.35)));          // R damped
                    px[i + 1] = ImageOps.Clamp((int)((v * 255) * (1 - tint * 0.18)));      // G slight
                    px[i + 2] = ImageOps.Clamp((int)(v * 255));                            // B full → cold blue
                }
            }
        }, ct);

        return context;
    }
}

/// <summary>热成像:温度伪彩 + 热源高亮。</summary>
public sealed class ThermalPreset : IStylePreset
{
    public string Id => "builtin.thermal";
    public string DisplayNameKey => "Preset.Thermal.Name";
    public string DescriptionKey => "Preset.Thermal.Description";
    public string? IconGlyph => "\uE9D9";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ThermalStage() };
}

public sealed class ThermalStage : StageBase
{
    public override string Name => "Thermal";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double contrast = Dbl(context.Parameters, "contrast", 0.5);

        var lut = ImageOps.ContrastLut(contrast);
        var luma = ImageOps.LumaMap(src);
        var blurred = (byte[])luma.Clone();
        ImageOps.BoxBlurGray(blurred, w, h, 1);

        // ironbow ramp stops (B, G, R)
        var stops = new List<((int B, int G, int R) Color, double Pos)>
        {
            ((2, 2, 20), 0.0),
            ((60, 10, 110), 0.25),
            ((230, 60, 40), 0.5),
            ((255, 180, 0), 0.75),
            ((255, 252, 230), 1.0),
        };

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                double t = lut[blurred[y * w + x]] / 255.0;
                int seg = 0;
                while (seg < stops.Count - 2 && t > stops[seg + 1].Pos) seg++;
                var a = stops[seg]; var b = stops[seg + 1];
                double f = (t - a.Pos) / (b.Pos - a.Pos);

                int i = y * stride + x * 4;
                px[i] = (byte)(a.Color.B + (b.Color.B - a.Color.B) * f);
                px[i + 1] = (byte)(a.Color.G + (b.Color.G - a.Color.G) * f);
                px[i + 2] = (byte)(a.Color.R + (b.Color.R - a.Color.R) * f);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>红外摄影:异常色彩 + 梦幻感。</summary>
public sealed class InfraredPreset : IStylePreset
{
    public string Id => "builtin.infrared";
    public string DisplayNameKey => "Preset.Infrared.Name";
    public string DescriptionKey => "Preset.Infrared.Description";
    public string? IconGlyph => "\uE7B3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("intensity", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new InfraredStage() };
}

public sealed class InfraredStage : StageBase
{
    public override string Name => "Infrared";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double intensity = Dbl(context.Parameters, "intensity", 0.6);

        var original = (byte[])px.Clone();

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                int b = original[i], g = original[i + 1], r = original[i + 2];

                // channel-mixing false color: foliage→pink/white, sky→deep cyan
                int nr = Math.Clamp((int)(r * 0.45 + (b + g) * 0.45 + 20), 0, 255);
                int ng = Math.Clamp((int)(b * 0.75 + g * 0.30), 0, 255);
                int nb = Math.Clamp((int)(r * 0.60 + b * 0.45), 0, 255);

                double mix = intensity;
                px[i] = ImageOps.Clamp((int)(b * (1 - mix) + nb * mix));
                px[i + 1] = ImageOps.Clamp((int)(g * (1 - mix) + ng * mix));
                px[i + 2] = ImageOps.Clamp((int)(r * (1 - mix) + nr * mix));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>双重曝光:镜像影像融合。</summary>
public sealed class DoubleExposurePreset : IStylePreset
{
    public string Id => "builtin.double-exposure";
    public string DisplayNameKey => "Preset.DoubleExposure.Name";
    public string DescriptionKey => "Preset.DoubleExposure.Description";
    public string? IconGlyph => "\uE8B4";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("offset", "Param.Generic.Offset", 0.25, 0.0, 0.5, 0.05),
        new PresetParameter("intensity", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new DoubleExposureStage() };
}

public sealed class DoubleExposureStage : StageBase
{
    public override string Name => "DoubleExposure";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double offset = Dbl(context.Parameters, "offset", 0.25);
        double intensity = Dbl(context.Parameters, "intensity", 0.6);

        var original = (byte[])px.Clone();
        int dx = (int)(w * offset);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                // mirrored + shifted ghost image
                int mx = Math.Clamp(w - 1 - (x + dx), 0, w - 1);
                int i = y * stride + x * 4;
                int j = y * stride + mx * 4;

                for (int c = 0; c < 3; c++)
                {
                    double a = original[i + c] / 255.0;
                    double b = original[j + c] / 255.0 * intensity;
                    double screen = 1.0 - (1.0 - a) * (1.0 - b); // screen blend
                    px[i + c] = ImageOps.Clamp((int)(screen * 255));
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>胶片负片:负片色彩 + 颗粒。</summary>
public sealed class FilmNegativePreset : IStylePreset
{
    public string Id => "builtin.film-negative";
    public string DisplayNameKey => "Preset.FilmNegative.Name";
    public string DescriptionKey => "Preset.FilmNegative.Description";
    public string? IconGlyph => "\uE8E9";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("warmth", "Param.Generic.Intensity", 0.4, 0.0, 1.0, 0.05),
        new PresetParameter("grain", "Param.Generic.Grain", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new FilmNegativeStage() };
}

public sealed class FilmNegativeStage : StageBase
{
    public override string Name => "FilmNegative";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double warmth = Dbl(context.Parameters, "warmth", 0.4);
        double grain = Dbl(context.Parameters, "grain", 0.5);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                // invert + orange film-base mask
                int nb = 255 - px[i];
                int ng = 255 - px[i + 1];
                int nr = 255 - px[i + 2];

                px[i] = ImageOps.Clamp((int)(nb * (1 - warmth * 0.25) + 30 * warmth));
                px[i + 1] = ImageOps.Clamp((int)(ng * (1 - warmth * 0.12) + 18 * warmth));
                px[i + 2] = ImageOps.Clamp((int)(nr * (1 - warmth * 0.05) + 60 * warmth));

                double n = (ImageOps.Hash(x, y, 17) - 0.5) * grain * 44;
                px[i] = ImageOps.Clamp(px[i] + (int)n);
                px[i + 1] = ImageOps.Clamp(px[i + 1] + (int)n);
                px[i + 2] = ImageOps.Clamp(px[i + 2] + (int)n);
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });
        return Task.FromResult(context);
    }
}

/// <summary>拍立得:褪色即成像 + 相纸质感。</summary>
public sealed class PolaroidPreset : IStylePreset
{
    public string Id => "builtin.polaroid";
    public string DisplayNameKey => "Preset.Polaroid.Name";
    public string DescriptionKey => "Preset.Polaroid.Description";
    public string? IconGlyph => "\uEB9F";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("fade", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("warmth", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PolaroidStage() };
}

public sealed class PolaroidStage : StageBase
{
    public override string Name => "Polaroid";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double fade = Dbl(context.Parameters, "fade", 0.5);
        double warmth = Dbl(context.Parameters, "warmth", 0.5);

        Parallel.For(0, h, new ParallelOptions { CancellationToken = ct }, y =>
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * stride + x * 4;
                for (int c = 0; c < 3; c++)
                {
                    double v = px[i + c] / 255.0;
                    v = 0.12 + v * (1 - 0.25 * fade);              // lifted blacks, lower contrast
                    px[i + c] = ImageOps.Clamp((int)(v * 255));
                }

                // warm chemical shift + gentle vignette
                double vig = 1.0 - 0.35 * fade *
                    Math.Pow(Math.Abs(x / (double)w - 0.5) * 2 * Math.Abs(y / (double)h - 0.5) * 2, 1.2);
                px[i] = ImageOps.Clamp((int)(px[i] * vig + 8 * warmth));
                px[i + 1] = ImageOps.Clamp((int)(px[i + 1] * vig + 3 * warmth));
                px[i + 2] = ImageOps.Clamp((int)(px[i + 2] * vig - 6 * warmth));
            }
            progress?.Report(new StageProgress(Name, 0, 1, (y + 1) / (double)h));
        });

        // white instant-film border
        int frame = Math.Min(w, h) / 14;
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                bool inFrame = x < frame || y < frame || x >= w - frame;
                bool bottom = y >= h - frame * 3;
                if (inFrame || bottom)
                {
                    int i = y * stride + x * 4;
                    px[i] = 246; px[i + 1] = 244; px[i + 2] = 238;
                }
            }
        }
        progress?.Report(new StageProgress(Name, 0, 1, 1.0));
        return Task.FromResult(context);
    }
}
