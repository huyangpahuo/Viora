using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.PluginSdk;

namespace Viora.Features.Convert.AnimeVector;

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
