using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>刺绣:线迹针脚构成图像。</summary>
public sealed class EmbroideryPreset : IStylePreset
{
    public string Id => "builtin.embroidery";
    public string DisplayNameKey => "Preset.Embroidery.Name";
    public string DescriptionKey => "Preset.Embroidery.Description";
    public string? IconGlyph => "\uE745";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("stitch", "Param.Generic.Size", 6, 3, 16, 1),
        new PresetParameter("colors", "Param.Generic.Colors", 8, 4, 16, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new EmbroideryStage() };
}

