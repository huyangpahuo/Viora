using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>报纸印刷:新闻纸 + 黑白油墨。</summary>
public sealed class NewspaperPreset : IStylePreset
{
    public string Id => "builtin.newspaper";
    public string DisplayNameKey => "Preset.Newspaper.Name";
    public string DescriptionKey => "Preset.Newspaper.Description";
    public string? IconGlyph => "\uE900";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter("grain", "Param.Generic.Grain", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new NewspaperStage() };
}

