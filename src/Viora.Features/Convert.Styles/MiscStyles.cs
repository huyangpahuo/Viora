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
        ImageOps.OpaqueAlpha(dst.Pixels);
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
        ImageOps.OpaqueAlpha(dst.Pixels);
        var outPx = dst.Pixels;
        int outStride = dst.Stride;

        double cx = w / 2.0, cy = h / 2.0;
        // isometric diamond: X = cx + (sx-sy)*halfW, Y = cy + (sx+sy)*halfH, sx/sy ∈ [-1,1]
        double halfW = w * 0.43 * scale;
        double halfH = h * 0.40 * scale;
        int thickness = (int)(Math.Min(w, h) * 0.035 * scale) + 3;

        // desk background: soft radial tone
        for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;
                double d = Math.Abs(x - cx) / cx + Math.Abs(y - cy) / cy;
                byte v = (byte)(46 + Math.Clamp(1.35 - d, 0, 1) * 26);
                outPx[i] = (byte)(v - 8); outPx[i + 1] = v; outPx[i + 2] = (byte)(v + 6);
            }

        var quad = new byte[4];
        int shOff = (int)(h * 0.03) + 2;

        // main pass: image diamond, extruded base walls, shadow
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int i = y * outStride + x * 4;

                // shadow (behind everything, slightly larger and shifted down)
                {
                    double dxs = (x - cx) / (halfW * 1.05);
                    double dys = (y - cy - shOff) / (halfH * 1.05);
                    double ssx = (dxs + dys) / 2, ssy = (dys - dxs) / 2;
                    if (Math.Abs(ssx) <= 1 && Math.Abs(ssy) <= 1)
                    {
                        double k = shadow * 0.5 * (1.1 - (Math.Abs(ssx) + Math.Abs(ssy)) / 2);
                        outPx[i] = ImageOps.Clamp((int)(outPx[i] * (1 - k) + 10 * k));
                        outPx[i + 1] = ImageOps.Clamp((int)(outPx[i + 1] * (1 - k) + 12 * k));
                        outPx[i + 2] = ImageOps.Clamp((int)(outPx[i + 2] * (1 - k) + 10 * k));
                    }
                }

                double dx = (x - cx) / halfW, dy = (y - cy) / halfH;
                double sx = (dx + dy) / 2, sy = (dy - dx) / 2;

                if (Math.Abs(sx) <= 1 && Math.Abs(sy) <= 1)
                {
                    // the picture itself, mapped onto the diamond floor
                    ImageOps.SampleBilinear(original, stride, w, h,
                        (sx * 0.5 + 0.5) * (w - 1), (sy * 0.5 + 0.5) * (h - 1), quad);
                    outPx[i] = quad[0]; outPx[i + 1] = quad[1]; outPx[i + 2] = quad[2];
                    continue;
                }

                // extruded base walls: pixels just below the diamond's lower edges
                double dy2 = (y - thickness - cy) / halfH;
                double sx2 = (dx + dy2) / 2, sy2 = (dy2 - dx) / 2;
                bool onRightWall = sx2 >= 1 && sx2 <= 1.15 && Math.Abs(sy2) <= 1;
                bool onLeftWall = sy2 >= 1 && sy2 <= 1.15 && Math.Abs(sx2) <= 1;
                if (onRightWall || onLeftWall)
                {
                    double uu = onRightWall ? 1 : sx2, vv = onLeftWall ? 1 : sy2;
                    double lu = (uu + vv) / 2, lv = (vv - uu) / 2;
                    if (lu >= -1 && lu <= 1 && lv >= -1 && lv <= 1)
                    {
                        ImageOps.SampleBilinear(original, stride, w, h,
                            (lu * 0.5 + 0.5) * (w - 1), (lv * 0.5 + 0.5) * (h - 1), quad);
                        outPx[i] = ImageOps.Clamp((int)(quad[0] * 0.42));
                        outPx[i + 1] = ImageOps.Clamp((int)(quad[1] * 0.42));
                        outPx[i + 2] = ImageOps.Clamp((int)(quad[2] * 0.42));
                    }
                }
            }
            progress?.Report(new StageProgress(Name, 0, 1, 0.3 + 0.7 * (y + 1) / (double)h));
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
            (new StipplingPreset(), "builtin.viora.stippling", "Stippling", "Tone built from dense ink dots."),
            (new CrossHatchingPreset(), "builtin.viora.cross-hatching", "Cross-Hatching", "Layered cross-hatch shading."),
            (new EtchingPreset(), "builtin.viora.etching", "Etching", "Fine engraved plate lines."),
            (new FrescoPreset(), "builtin.viora.fresco", "Fresco", "Chalky mineral pigment on plaster."),
            (new MosaicGlassPreset(), "builtin.viora.mosaic-glass", "Mosaic Glass", "Irregular glass shards with sparkle."),
            (new MetalEngravingPreset(), "builtin.viora.metal-engraving", "Metal Engraving", "Bright engraved lines on steel."),
            (new NeonSignPreset(), "builtin.viora.neon-sign", "Neon Sign", "Glowing tube outlines on a night wall."),
            (new LiquidMetalPreset(), "builtin.viora.liquid-metal", "Liquid Metal", "Molten chrome with flowing reflections."),
            (new ChromePreset(), "builtin.viora.chrome", "Chrome", "Mirror-finish chrome reflection bands."),
            (new GlowingWireframePreset(), "builtin.viora.glowing-wireframe", "Glowing Wireframe", "Luminous grid and edge lines."),
            (new TornPaperPreset(), "builtin.viora.torn-paper", "Torn Paper", "Stacked strips with torn edges."),
            (new TapeArtPreset(), "builtin.viora.tape-art", "Tape Art", "Colored tape strips forming the image."),
            (new StringArtPreset(), "builtin.viora.string-art", "String Art", "Crossing threads drawing the contours."),
            (new SandArtPreset(), "builtin.viora.sand-art", "Sand Art", "Backlit sand grains forming the image."),
            (new SmokeArtPreset(), "builtin.viora.smoke-art", "Smoke Art", "Soft drifting smoke silhouettes."),
            (new LightPaintingPreset(), "builtin.viora.light-painting", "Light Painting", "Long-exposure glowing light trails."),
            (new KaleidoscopePreset(), "builtin.viora.kaleidoscope", "Kaleidoscope", "Mirrored radial symmetry."),
            (new LiquidMarblePreset(), "builtin.viora.liquid-marble", "Liquid Marble", "Marbled swirls with stone veins."),
            (new DitheredPreset(), "builtin.viora.dithered", "Dithered", "Ordered dithering into a tiny palette."),
            (new GlobeReliefPreset(), "builtin.viora.globe-relief", "Relief", "Embossed raised-surface render."),
        };
}
