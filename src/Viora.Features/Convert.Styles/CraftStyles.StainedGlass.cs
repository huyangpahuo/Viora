using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.Styles.Core;
using Viora.PluginSdk;

namespace Viora.Features.Convert.Styles;


/// <summary>彩色玻璃:玻璃色块 + 铅条分割。</summary>
public sealed class StainedGlassPreset : IStylePreset
{
    public string Id => "builtin.stained-glass";
    public string DisplayNameKey => "Preset.StainedGlass.Name";
    public string DescriptionKey => "Preset.StainedGlass.Description";
    public string? IconGlyph => "\uEA3A";
    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter("cellSize", "Param.Generic.Size", 20, 8, 48, 2),
        new PresetParameter("lead", "Param.Generic.Thickness", 2, 1, 6, 1),
    };
    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> p) => new IImageProcessingStage[] { new StainedGlassStage() };
}

