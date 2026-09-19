using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>折纸:折痕结构 + 几何立体。</summary>
public sealed class OrigamiPreset : IStylePreset
{
    public string Id => "builtin.origami";
    public string DisplayNameKey => "Preset.Origami.Name";
    public string DescriptionKey => "Preset.Origami.Description";
    public string? IconGlyph => "\uE8A5";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 28, 12, 64, 2),
        new PresetParameter("crease", "Param.Generic.Intensity", 0.5, 0.0, 1.0, 0.05),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new OrigamiStage() };
}

