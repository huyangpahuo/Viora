using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>木刻版画:雕刻线条 + 高反差。</summary>
public sealed class WoodcutPreset : IStylePreset
{
    public string Id => "builtin.woodcut";
    public string DisplayNameKey => "Preset.Woodcut.Name";
    public string DescriptionKey => "Preset.Woodcut.Description";
    public string? IconGlyph => "\uEDA0";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("contrast", "Param.Generic.Contrast", 0.7, 0.0, 1.0, 0.05),
        new PresetParameter("density", "Param.Generic.Density", 0.6, 0.1, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new WoodcutStage() };
}

