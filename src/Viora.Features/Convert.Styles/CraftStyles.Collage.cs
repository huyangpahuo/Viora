using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>拼贴艺术:素材撕贴组合。</summary>
public sealed class CollagePreset : IStylePreset
{
    public string Id => "builtin.collage";
    public string DisplayNameKey => "Preset.Collage.Name";
    public string DescriptionKey => "Preset.Collage.Description";
    public string? IconGlyph => "\uE8B2";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("tiles", "Param.Generic.Density", 4, 2, 6, 1),
        new PresetParameter("rotation", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new CollageStage() };
}

