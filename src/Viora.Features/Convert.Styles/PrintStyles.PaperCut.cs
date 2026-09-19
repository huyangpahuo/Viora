using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>剪纸:多层纸片 + 叠层阴影。</summary>
public sealed class PaperCutPreset : IStylePreset
{
    public string Id => "builtin.paper-cut";
    public string DisplayNameKey => "Preset.PaperCut.Name";
    public string DescriptionKey => "Preset.PaperCut.Description";
    public string? IconGlyph => "\uE8D9";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("layers", "Param.Generic.Colors", 5, 3, 8, 1),
        new PresetParameter("offset", "Param.Generic.Offset", 2, 1, 6, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new PaperCutStage() };
}

