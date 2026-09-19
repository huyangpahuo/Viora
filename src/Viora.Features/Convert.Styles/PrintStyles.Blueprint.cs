using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


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

