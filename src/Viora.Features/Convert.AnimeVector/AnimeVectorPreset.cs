using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Features.Convert.Common;
using Viora.Features.Convert.AnimeVector.Stages;
using Viora.PluginSdk;

/// <summary>
/// The Anime Vector style preset: composes the pipeline from the Phase 0 Style Analysis:
/// Preprocess → Smooth → Quantize → Consolidate → ShadowBlock → EdgeInk.
/// Parameters map 1:1 to the five style rules; defaults reflect the reference measurements.
/// </summary>
public sealed class AnimeVectorPreset : IStylePreset
{
    public const string PresetId = "builtin.anime-vector";

    public string Id => PresetId;

    public string DisplayNameKey => "Preset.AnimeVector.Name";

    public string DescriptionKey => "Preset.AnimeVector.Description";

    public string? IconGlyph => "\uE790"; // Segoe UI "Color" glyph

    public IReadOnlyList<IPresetParameter> Parameters { get; } = new IPresetParameter[]
    {
        new PresetParameter(ParamKeys.Colors, "Param.Colors", 8, 2, 24, 1),
        new PresetParameter(ParamKeys.Smoothing, "Param.Smoothing", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter(ParamKeys.Detail, "Param.Detail", 0.5, 0.0, 1.0, 0.05),
        new PresetParameter(ParamKeys.Shadows, "Param.Shadows", 0.6, 0.0, 1.0, 0.05),
        new PresetParameter(ParamKeys.Edges, "Param.Edges", 0.25, 0.0, 1.0, 0.05),
        new PresetParameter(ParamKeys.Saturation, "Param.Saturation", 1.15, 1.0, 1.6, 0.05),
    };

    public IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters)
        => new IImageProcessingStage[]
        {
            new Viora.Features.Convert.AnimeVector.Stages.PreprocessStage(),
            new Viora.Features.Convert.AnimeVector.Stages.SmoothStage(),
            new Viora.Features.Convert.AnimeVector.Stages.QuantizeStage(),
            new Viora.Features.Convert.AnimeVector.Stages.ConsolidateStage(),
            new Viora.Features.Convert.AnimeVector.Stages.ShadowBlockStage(),
            new Viora.Features.Convert.AnimeVector.Stages.EdgeInkStage(),
        };
}
