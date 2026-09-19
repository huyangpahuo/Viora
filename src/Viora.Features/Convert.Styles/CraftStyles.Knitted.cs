using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


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

