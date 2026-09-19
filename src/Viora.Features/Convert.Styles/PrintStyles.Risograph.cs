using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>孔版印刷:双色油墨 + 套色错位 + 颗粒。</summary>
public sealed class RisographPreset : IStylePreset
{
    public string Id => "builtin.risograph";
    public string DisplayNameKey => "Preset.Risograph.Name";
    public string DescriptionKey => "Preset.Risograph.Description";
    public string? IconGlyph => "\uE8FD";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("offset", "Param.Generic.Offset", 2, 0, 6, 1),
        new PresetParameter("grain", "Param.Generic.Grain", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new RisographStage() };
}

