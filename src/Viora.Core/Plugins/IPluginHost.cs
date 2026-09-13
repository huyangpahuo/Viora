using Viora.Core.Pipeline;

namespace Viora.Core.Plugins;

public sealed record InstallResult(bool Success, string? ErrorCode, string? Detail);

public sealed record PluginLoadResult(
    bool Success,
    string? PluginId,
    string? ErrorMessage);

/// <summary>
/// Registration surface granted to a plugin (or built-in feature) after capability checks.
/// Plugins never receive raw filesystem/registry access through this interface.
/// </summary>
public interface IPluginContext
{
    void RegisterPreset(IStylePreset preset);

    void RegisterExporter(IImageExporter exporter);

    void RegisterSettingsPage(ISettingsPageDescriptor page);

    void RegisterStrings(IReadOnlyDictionary<string, string> stringsByLanguageAndKey);

    Microsoft.Extensions.Logging.ILogger Logger { get; }
}

/// <summary>Export format contract (PNG/JPEG/SVG today; plugin-extensible).</summary>
public interface IImageExporter
{
    string FormatId { get; }

    string DisplayNameKey { get; }

    /// <summary>File extension including the dot, e.g. ".png".</summary>
    string FileExtension { get; }

    Task ExportAsync(
        Viora.Core.Imaging.IImageBuffer buffer,
        System.IO.Stream output,
        IReadOnlyDictionary<string, object>? options,
        CancellationToken cancellationToken);
}

/// <summary>Descriptor for a plugin-provided settings page (UI renders it via the UI layer).</summary>
public interface ISettingsPageDescriptor
{
    string Id { get; }

    string TitleKey { get; }

    string? OwnerPluginId { get; }
}

public interface IPluginHost
{
    Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default);

    Task<PluginLoadResult> LoadAsync(string pluginId, CancellationToken cancellationToken = default);

    Task UnloadAsync(string pluginId, CancellationToken cancellationToken = default);

    Task EnableAsync(string pluginId, CancellationToken cancellationToken = default);

    Task DisableAsync(string pluginId, CancellationToken cancellationToken = default);

    Task<InstallResult> InstallFromPackageAsync(string packagePath, CancellationToken cancellationToken = default);

    Task UninstallAsync(string pluginId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PluginDescriptor>> GetPluginsAsync(CancellationToken cancellationToken = default);

    event EventHandler<PluginDescriptor>? PluginStateChanged;
}
