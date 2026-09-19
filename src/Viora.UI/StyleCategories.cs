namespace Viora.UI;

/// <summary>
/// 风格分类体系:20 个固定大类。内置预设按 id 预映射,创作者未来发布插件时从中选择,
/// 避免“其他”这类模糊归类。键通过本地化 Style.Cat.* 显示。
/// </summary>
public static class StyleCategories
{
    public static readonly string[] AllKeys =
    {
        "Style.Cat.Anime", "Style.Cat.Illustration", "Style.Cat.OilPainting", "Style.Cat.Watercolor",
        "Style.Cat.Sketch", "Style.Cat.Printmaking", "Style.Cat.Pixel", "Style.Cat.RetroFilm",
        "Style.Cat.ThreeD", "Style.Cat.Cyberpunk", "Style.Cat.Neon", "Style.Cat.Handcraft",
        "Style.Cat.PaperCraft", "Style.Cat.Collage", "Style.Cat.Photography", "Style.Cat.GlassMosaic",
        "Style.Cat.Metallic", "Style.Cat.LightParticles", "Style.Cat.Traditional", "Style.Cat.Experimental",
    };

    /// <summary>内置预设 → 分类(键为去掉 builtin. 前缀的短 id)。</summary>
    private static readonly Dictionary<string, string> ByPresetId = new(StringComparer.OrdinalIgnoreCase)
    {
        ["anime-vector"] = "Style.Cat.Anime", ["comic"] = "Style.Cat.Anime",
        ["oil-painting"] = "Style.Cat.OilPainting", ["fresco"] = "Style.Cat.OilPainting",
        ["watercolor"] = "Style.Cat.Watercolor",
        ["sketch"] = "Style.Cat.Sketch", ["stippling"] = "Style.Cat.Sketch", ["cross-hatching"] = "Style.Cat.Sketch",
        ["woodcut"] = "Style.Cat.Printmaking", ["etching"] = "Style.Cat.Printmaking",
        ["metal-engraving"] = "Style.Cat.Printmaking", ["risograph"] = "Style.Cat.Printmaking",
        ["newspaper"] = "Style.Cat.Printmaking",
        ["pixel-art"] = "Style.Cat.Pixel", ["dithered"] = "Style.Cat.Pixel",
        ["crt"] = "Style.Cat.RetroFilm", ["film-negative"] = "Style.Cat.RetroFilm",
        ["halftone"] = "Style.Cat.RetroFilm", ["polaroid"] = "Style.Cat.RetroFilm",
        ["low-poly"] = "Style.Cat.ThreeD", ["clay"] = "Style.Cat.ThreeD",
        ["isometric-diorama"] = "Style.Cat.ThreeD", ["globe-relief"] = "Style.Cat.ThreeD",
        ["neon-cyberpunk"] = "Style.Cat.Cyberpunk", ["glitch"] = "Style.Cat.Cyberpunk",
        ["neon-sign"] = "Style.Cat.Neon", ["holographic"] = "Style.Cat.Neon",
        ["glowing-wireframe"] = "Style.Cat.Neon",
        ["knitted"] = "Style.Cat.Handcraft", ["embroidery"] = "Style.Cat.Handcraft",
        ["wax-crayon"] = "Style.Cat.Handcraft", ["string-art"] = "Style.Cat.Handcraft",
        ["porcelain"] = "Style.Cat.Handcraft",
        ["origami"] = "Style.Cat.PaperCraft", ["paper-cut"] = "Style.Cat.PaperCraft",
        ["tape-art"] = "Style.Cat.PaperCraft", ["torn-paper"] = "Style.Cat.PaperCraft",
        ["chalkboard"] = "Style.Cat.PaperCraft", ["blueprint"] = "Style.Cat.PaperCraft",
        ["collage"] = "Style.Cat.Collage",
        ["infrared"] = "Style.Cat.Photography", ["thermal"] = "Style.Cat.Photography",
        ["xray"] = "Style.Cat.Photography", ["double-exposure"] = "Style.Cat.Photography",
        ["stained-glass"] = "Style.Cat.GlassMosaic", ["mosaic"] = "Style.Cat.GlassMosaic",
        ["mosaic-glass"] = "Style.Cat.GlassMosaic", ["kaleidoscope"] = "Style.Cat.GlassMosaic",
        ["chrome"] = "Style.Cat.Metallic", ["liquid-metal"] = "Style.Cat.Metallic",
        ["liquid-marble"] = "Style.Cat.Metallic",
        ["light-painting"] = "Style.Cat.LightParticles", ["smoke-art"] = "Style.Cat.LightParticles",
        ["sand-art"] = "Style.Cat.LightParticles",
        ["ascii-art"] = "Style.Cat.Experimental",
    };

    /// <summary>按预设 id 解析分类(兼容 builtin. 前缀);未匹配归入实验创意。</summary>
    public static string ResolveCategoryKey(string presetId)
    {
        var bare = presetId.StartsWith("builtin.", StringComparison.OrdinalIgnoreCase)
            ? presetId["builtin.".Length..]
            : presetId;
        return ByPresetId.TryGetValue(bare, out var key) ? key : "Style.Cat.Experimental";
    }
}
