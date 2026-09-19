using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>低多边形:三角面切割 + 几何光影。</summary>
public sealed class LowPolyPreset : IStylePreset
{
    public string Id => "builtin.low-poly";
    public string DisplayNameKey => "Preset.LowPoly.Name";
    public string DescriptionKey => "Preset.LowPoly.Description";
    public string? IconGlyph => "\uF0E4";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 24, 10, 64, 2),
        new PresetParameter("jitter", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new LowPolyStage() };
}

