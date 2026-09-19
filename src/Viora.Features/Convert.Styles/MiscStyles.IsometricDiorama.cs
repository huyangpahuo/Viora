using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


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

