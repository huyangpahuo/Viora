using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>黑板粉笔:粉笔颗粒 + 教学感。</summary>
public sealed class ChalkboardPreset : IStylePreset
{
    public string Id => "builtin.chalkboard";
    public string DisplayNameKey => "Preset.Chalkboard.Name";
    public string DescriptionKey => "Preset.Chalkboard.Description";
    public string? IconGlyph => "\uE7C3";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter("dust", "Param.Generic.Texture", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ChalkboardStage() };
}

