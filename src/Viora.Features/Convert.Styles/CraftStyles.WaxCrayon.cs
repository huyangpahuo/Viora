using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>蜡笔:蜡质笔触 + 童趣涂抹。</summary>
public sealed class WaxCrayonPreset : IStylePreset
{
    public string Id => "builtin.wax-crayon";
    public string DisplayNameKey => "Preset.WaxCrayon.Name";
    public string DescriptionKey => "Preset.WaxCrayon.Description";
    public string? IconGlyph => "\uE7C3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("stroke", "Param.Generic.Size", 8, 3, 20, 1),
        new PresetParameter("pressure", "Param.Generic.Intensity", 0.6, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new WaxCrayonStage() };
}

