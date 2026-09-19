using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>黏土雕塑:圆润体积 + 手工模型感。</summary>
public sealed class ClayPreset : IStylePreset
{
    public string Id => "builtin.clay";
    public string DisplayNameKey => "Preset.Clay.Name";
    public string DescriptionKey => "Preset.Clay.Description";
    public string? IconGlyph => "\uE7F1";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("smoothing", "Param.Generic.Intensity", 0.7, 0.0, 1.0, 0.05),
        new PresetParameter("relief", "Param.Generic.Texture", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ClayStage() };
}

