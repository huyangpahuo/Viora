using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>瓷器:釉面反光 + 精致质感。</summary>
public sealed class PorcelainPreset : IStylePreset
{
    public string Id => "builtin.porcelain";
    public string DisplayNameKey => "Preset.Porcelain.Name";
    public string DescriptionKey => "Preset.Porcelain.Description";
    public string? IconGlyph => "\uE80A";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("gloss", "Param.Generic.Glow", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("smoothing", "Param.Generic.Intensity", 0.7, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PorcelainStage() };
}

