using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>漫画:粗黑描边 + 海报化色块 + 硬朗阴影。</summary>
public sealed class ComicPreset : IStylePreset
{
    public string Id => "builtin.comic";
    public string DisplayNameKey => "Preset.Comic.Name";
    public string DescriptionKey => "Preset.Comic.Description";
    public string? IconGlyph => "\uE90C";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("colors", "Param.Generic.Colors", 5, 3, 8, 1),
        new PresetParameter("thickness", "Param.Generic.Thickness", 2, 1, 4, 1),
        new PresetParameter("detail", "Param.Generic.Detail", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new ComicStage() };
}

