using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>半色调:印刷网点明暗。</summary>
public sealed class HalftonePreset : IStylePreset
{
    public string Id => "builtin.halftone";
    public string DisplayNameKey => "Preset.Halftone.Name";
    public string DescriptionKey => "Preset.Halftone.Description";
    public string? IconGlyph => "\uE7A8";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 8, 4, 24, 1),
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new HalftoneStage() };
}

