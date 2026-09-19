using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


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

