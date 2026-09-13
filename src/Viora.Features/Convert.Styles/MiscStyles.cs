using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;

// ============ Group D: misc styles ============
// ASCII art + isometric diorama, plus the central registry consumed by DI and tests.

/// <summary>ASCII 字符画:字符密度构成明暗。</summary>
public sealed class AsciiArtPreset : IStylePreset
{
    public string Id => "builtin.ascii-art";
    public string DisplayNameKey => "Preset.AsciiArt.Name";
    public string DescriptionKey => "Preset.AsciiArt.Description";
    public string? IconGlyph => "\uEA37";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 10, 6, 24, 1),
        new PresetParameter("invert", "Param.Generic.Intensity", 0.0, 0.0, 1.0, 1.0),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new AsciiArtStage() };
}

public sealed class AsciiArtStage : StageBase
{
    public override string Name => "AsciiArt";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height;
        int cell = Math.Clamp(Int(context.Parameters, "cellSize", 10), 6, 32);
        bool invert = Dbl(context.Parameters, "invert", 0.0) > 0.5;

        var luma = ImageOps.LumaMap(src);

        var dst = new RgbaImageBuffer(w, h);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;
        int cols = (w + cell - 1) / cell, rows = (h + cell - 1) / cell;

        // terminal background
        for (int i = 0; i < outPx.Length; i += 4)
        {
            outPx[i] = 16; outPx[i + 1] = 17; outPx[i + 2] = 15; outPx[i + 3] = 255;
        }

        for (int cy = 0; cy < rows; cy++)
        {
            for (int cx = 0; cx < cols; cx++)
            {
                long sum = 0; int n = 0;
                int x1 = Math.Min(w, (cx + 1) * cell), y1 = Math.Min(h, (cy + 1) * cell);
                for (int y = cy * cell; y < y1; y++)
                    for (int x = cx * cell; x < x1; x++)
                    {
                        sum += luma[y * w + x]; n++;
                    }

                double t = invert ? 1.0 - sum / (double)(n * 255) : sum / (double)(n * 255);
                int glyph = Math.Clamp((int)Math.Round(t * (ImageOps.AsciiChars.Length - 1)), 0, ImageOps.AsciiChars.Length - 1);
                if (glyph == 0) continue;

                // character color ramps with brightness (terminal green)
                byte b = (byte)(90 + t * 60), g = (byte)(140 + t * 110), r = (byte)(60 + t * 70);

                for (int gy = 0; gy < 7; gy++)
                {
                    int py = cy * cell + gy * cell / 7;
                    if (py >= h) break;
                    for (int gx = 0; gx < 5; gx++)
                    {
                        int pxc = cx * cell + gx * cell / 5;
                        if (pxc >= w) break;
                        if (!ImageOps.AsciiBit(glyph, gx, gy)) continue;

                        int i = py * outStride + pxc * 4;
                        outPx[i] = b; outPx[i + 1] = g; outPx[i + 2] = r;
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, (cy + 1) / (double)rows));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>等距微缩场景:等距视角 + 微缩模型感。</summary>
public sealed class IsometricDioramaPreset : IStylePreset
{
    public string Id => "builtin.isometric-diorama";
    public string DisplayNameKey => "Preset.IsometricDiorama.Name";
    public string DescriptionKey => "Preset.IsometricDiorama.Description";
    public string? IconGlyph => "\uE7F6";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("scale", "Param.Generic.Size", 0.62, 0.35, 0.9, 0.05),
        new PresetParameter("shadow", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new IsometricDioramaStage() };
}

public sealed class IsometricDioramaStage : StageBase
{
    public override string Name => "IsometricDiorama";
    public override Task<ImageProcessingContext> ExecuteAsync(ImageProcessingContext context, IProgress<StageProgress>? progress, CancellationToken ct)
    {
        var src = context.Working!;
        int w = src.Width, h = src.Height, stride = src.Stride;
        var px = src.Pixels;
        double scale = Dbl(context.Parameters, "scale", 0.62);
        double shadow = Dbl(context.Parameters, "shadow", 0.5);

        var original = (byte[])px.Clone();
        var dst = new RgbaImageBuffer(w, h);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        double cx = w / 2.0, cy = h / 2.0;
        // isometric: rotate 45° then squash vertically by ~0.575 (cos of the tilt)
        double halfW = w * scale * 0.5, halfH = h * scale * 0.5;
        const double squash = 0.575;

        // desk background: soft radial desk tone
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                double d = Math.Abs(x - cx) / cx + Math.Abs(y - cy) / cy;
                byte v = (byte)(46 + Math.Clamp(1.35 - d, 0, 1) * 26);
                outPx[i] = (byte)(v - 8); outPx[i + 1] = v; outPx[i + 2] = (byte)(v + 6);
            }

        // soft shadow diamond under the model
        int shadowB = 8, shadowG = 10, shadowR = 8;
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                double u = (x - cx) / (halfW * 1.06), v = (y - cy - h * 0.03) / (halfH * 1.06);
                double dist = Math.Abs(u) + Math.Abs(v);
                if (dist > 1) continue;
                int i = y * outStride + x * 4;
                double k = shadow * (1 - dist) * 0.55;
                outPx[i] = ImageOps.Clamp((int)(outPx[i] * (1 - k) + shadowB * k));
                outPx[i + 1] = ImageOps.Clamp((int)(outPx[i + 1] * (1 - k) + shadowG * k));
                outPx[i + 2] = ImageOps.Clamp((int)(outPx[i + 2] * (1 - k) + shadowR * k));
            }
        progress?.Report(new StageProgress(Name, 0, 1, 0.4));

        // inverse isometric map into the source image
        var quad = new byte[4];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                double u = (x - cx) / halfW, v = (y - cy) / (halfH * squash * 2);
                double su = (u + v) / 2, sv = (v - u) / 2;
                if (Math.Abs(su) > 1 || Math.Abs(sv) > 1) continue;

                double sx = (su * 0.5 + 0.5) * (w - 1);
                double sy = (sv * 0.5 + 0.5) * (h - 1);
                ImageOps.SampleBilinear(original, stride, w, h, sx, sy, quad);

                int i = y * outStride + x * 4;
                outPx[i] = quad[0]; outPx[i + 1] = quad[1]; outPx[i + 2] = quad[2];
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.4 + 0.6 * (y + 1) / (double)h));
        }

        context.Working = dst;
        return Task.FromResult(context);
    }
}

/// <summary>
/// 30 个新增内置风格的集中注册表:DI 注册与管线冒烟测试共用这一份清单。
/// </summary>
public static class BuiltInStyles
{
    public static IReadOnlyList<(IStylePreset Preset, string FeatureId, string DisplayName, string Description)> All { get; } =
        new List<(IStylePreset, string, string, string)>
        {
            (new ComicPreset(), "builtin.viora.comic", "Comic Book", "Comic book stylization."),
            (new PixelArtPreset(), "builtin.viora.pixel-art", "Pixel Art", "Low-resolution pixel art reconstruction."),
            (new LowPolyPreset(), "builtin.viora.low-poly", "Low Poly", "Faceted low-poly reconstruction."),
            (new ClayPreset(), "builtin.viora.clay", "Clay Sculpture", "Rounded clay-model look."),
            (new PaperCutPreset(), "builtin.viora.paper-cut", "Paper Cut", "Layered paper-cut look."),
            (new WoodcutPreset(), "builtin.viora.woodcut", "Woodcut", "Carved woodcut print look."),
            (new RisographPreset(), "builtin.viora.risograph", "Risograph", "Two-ink riso print with misregistration."),
            (new HalftonePreset(), "builtin.viora.halftone", "Halftone", "Classic print halftone dots."),
            (new NewspaperPreset(), "builtin.viora.newspaper", "Newspaper", "Aged newsprint look."),
            (new BlueprintPreset(), "builtin.viora.blueprint", "Blueprint", "Technical drafting blueprint."),
            (new NeonCyberpunkPreset(), "builtin.viora.neon-cyberpunk", "Neon Cyberpunk", "Neon glow cyberpunk grade."),
            (new GlitchPreset(), "builtin.viora.glitch", "Glitch Art", "RGB-split digital glitch."),
            (new CRTPreset(), "builtin.viora.crt", "CRT Retro", "CRT scanline retro screen."),
            (new HolographicPreset(), "builtin.viora.holographic", "Holographic", "Iridescent holographic foil."),
            (new XRayPreset(), "builtin.viora.xray", "X-Ray", "Cold medical X-ray look."),
            (new ThermalPreset(), "builtin.viora.thermal", "Thermal Camera", "Ironbow thermal imaging."),
            (new InfraredPreset(), "builtin.viora.infrared", "Infrared", "Infrared false-color photography."),
            (new DoubleExposurePreset(), "builtin.viora.double-exposure", "Double Exposure", "Mirrored double exposure."),
            (new FilmNegativePreset(), "builtin.viora.film-negative", "Film Negative", "Darkroom film negative."),
            (new PolaroidPreset(), "builtin.viora.polaroid", "Polaroid", "Faded instant photo with frame."),
            (new CollagePreset(), "builtin.viora.collage", "Collage", "Torn-paper collage assembly."),
            (new EmbroideryPreset(), "builtin.viora.embroidery", "Embroidery", "Stitch-thread embroidery look."),
            (new KnittedPreset(), "builtin.viora.knitted", "Knitted", "Soft knitted wool pattern."),
            (new StainedGlassPreset(), "builtin.viora.stained-glass", "Stained Glass", "Leaded stained-glass panes."),
            (new PorcelainPreset(), "builtin.viora.porcelain", "Porcelain", "Glazed porcelain sheen."),
            (new OrigamiPreset(), "builtin.viora.origami", "Origami", "Creased folded-paper facets."),
            (new ChalkboardPreset(), "builtin.viora.chalkboard", "Chalkboard", "Chalk strokes on a blackboard."),
            (new WaxCrayonPreset(), "builtin.viora.wax-crayon", "Wax Crayon", "Waxy crayon-on-paper strokes."),
            (new AsciiArtPreset(), "builtin.viora.ascii-art", "ASCII Art", "Character-density ASCII render."),
            (new IsometricDioramaPreset(), "builtin.viora.isometric-diorama", "Isometric Diorama", "Miniature isometric diorama."),
        };
}
