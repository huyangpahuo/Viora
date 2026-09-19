using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


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

