using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Features.Convert.Common;
using Viora.PluginSdk;
using Viora.UI.Hosting;

namespace Viora.Features.Convert.AnimeVector;

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
            new Stages.PreprocessStage(),
            new Stages.SmoothStage(),
            new Stages.QuantizeStage(),
            new Stages.ConsolidateStage(),
            new Stages.ShadowBlockStage(),
            new Stages.EdgeInkStage(),
        };
}

/// <summary>
/// Built-in feature wrapper: registers the preset + raster exporters through the
/// IPluginContext seam (the same path external plugins use).
/// </summary>
public sealed class AnimeVectorFeature : IBuiltInFeature
{
    public PluginMetadata Metadata { get; } = new(
        Id: "builtin.viora.anime-vector",
        DisplayName: "Anime Vector",
        Version: new Version(1, 0, 0),
        Author: "Viora Project",
        Description: "Flat geometric anime / vector illustration conversion.",
        Homepage: null,
        Repository: null,
        RequiredHostVersion: Core.Plugins.VersionRange.Any,
        Capabilities: new[]
        {
            PluginContextRegistration.PresetCapability,
            PluginContextRegistration.ExporterCapability,
            PluginContextRegistration.StringsCapability,
        },
        Dependencies: Array.Empty<PluginDependency>());

    public Task InitializeAsync(IPluginContext context, CancellationToken cancellationToken)
    {
        context.RegisterPreset(new AnimeVectorPreset());
        context.RegisterExporter(new InfrastructureExportBridge.PngExporterProxy());
        context.RegisterExporter(new InfrastructureExportBridge.JpegExporterProxy());
        context.RegisterExporter(new InfrastructureExportBridge.BmpExporterProxy());
        return Task.CompletedTask;
    }
}

/// <summary>Capability token constants shared by the feature host bridge.</summary>
public static class PluginContextRegistration
{
    public const string PresetCapability = "convert.preset";
    public const string ExporterCapability = "export.format";
    public const string StringsCapability = "localization.strings";
}

/// <summary>
/// The Infrastructure raster exporters exposed to the catalog. Kept in this assembly as thin
/// delegates so Viora.Features itself stays WPF-free (exporters resolve through the UI/host layer).
/// </summary>
public static class InfrastructureExportBridge
{
    public sealed class PngExporterProxy : ProxyExporter
    {
        public PngExporterProxy() : base("png", "Export.Format.Png", ".png", WicFormat.Png) { }
    }

    public sealed class JpegExporterProxy : ProxyExporter
    {
        public JpegExporterProxy() : base("jpeg", "Export.Format.Jpeg", ".jpg", WicFormat.Jpeg) { }
    }

    public sealed class BmpExporterProxy : ProxyExporter
    {
        public BmpExporterProxy() : base("bmp", "Export.Format.Bmp", ".bmp", WicFormat.Bmp) { }
    }

    public enum WicFormat { Png, Jpeg, Bmp }

    public abstract class ProxyExporter : IImageExporter
    {
        private readonly WicFormat _format;

        protected ProxyExporter(string formatId, string displayNameKey, string extension, WicFormat format)
        {
            FormatId = formatId;
            DisplayNameKey = displayNameKey;
            FileExtension = extension;
            _format = format;
        }

        public string FormatId { get; }

        public string DisplayNameKey { get; }

        public string FileExtension { get; }

        public Task ExportAsync(Core.Imaging.IImageBuffer buffer, Stream output, IReadOnlyDictionary<string, object>? options, CancellationToken cancellationToken)
            => HostExportBridge.ExportAsync(buffer, output, _format, options, cancellationToken);
    }
}

/// <summary>
/// Static bridge resolved at runtime by the host (Viora.App) so Features never references
/// PresentationCore directly; the host injects the real exporter delegate at startup.
/// </summary>
public static class HostExportBridge
{
    public static Func<Core.Imaging.IImageBuffer, Stream, WicFormatFromFeatures, IReadOnlyDictionary<string, object>?, CancellationToken, Task> Export { get; set; } =
        (_, _, _, _, _) => throw new InvalidOperationException("Export bridge not initialized by host.");

    // Alias so the delegate signature doesn't need a using of the nested enum type.
    public static Task ExportAsync(
        Core.Imaging.IImageBuffer buffer, Stream output,
        InfrastructureExportBridge.WicFormat format,
        IReadOnlyDictionary<string, object>? options,
        CancellationToken cancellationToken)
        => Export(buffer, output, (WicFormatFromFeatures)(int)format, options, cancellationToken);

    public enum WicFormatFromFeatures { Png, Jpeg, Bmp }
}
