using System.IO;
using Microsoft.Extensions.Logging;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Settings;

namespace Viora.Infrastructure.Plugins;

/// <summary>
/// The host-side IPluginContext: collects registrations from a plugin (or built-in
/// feature) behind capability checks. Plugins never receive broader services.
/// </summary>
public sealed class PluginContext : IPluginContext
{
    public const string CapabilityPreset = "convert.preset";
    public const string CapabilityExporter = "export.format";
    public const string CapabilitySettingsPage = "ui.panel";
    public const string CapabilityStrings = "localization.strings";

    private readonly string? _pluginId;
    private readonly IReadOnlyList<string> _capabilities;
    private readonly Action<IStylePreset> _addPreset;
    private readonly Action<IImageExporter> _addExporter;
    private readonly Action<ISettingsPageDescriptor> _addSettingsPage;
    private readonly Action<IReadOnlyDictionary<string, string>> _addStrings;

    public PluginContext(
        string? pluginId,
        IReadOnlyList<string> capabilities,
        ILogger logger,
        Action<IStylePreset> addPreset,
        Action<IImageExporter> addExporter,
        Action<ISettingsPageDescriptor> addSettingsPage,
        Action<IReadOnlyDictionary<string, string>> addStrings)
    {
        _pluginId = pluginId;
        _capabilities = capabilities;
        Logger = logger;
        _addPreset = addPreset;
        _addExporter = addExporter;
        _addSettingsPage = addSettingsPage;
        _addStrings = addStrings;
    }

    public ILogger Logger { get; }

    private bool Has(string capability) =>
        _pluginId is null || _capabilities.Contains(capability); // built-ins: all caps

    public void RegisterPreset(IStylePreset preset)
    {
        if (!Has(CapabilityPreset))
        {
            Logger.LogWarning("Plugin {Plugin} tried to register a preset without capability", _pluginId);
            return;
        }
        _addPreset(preset);
    }

    public void RegisterExporter(IImageExporter exporter)
    {
        if (!Has(CapabilityExporter))
        {
            Logger.LogWarning("Plugin {Plugin} tried to register an exporter without capability", _pluginId);
            return;
        }
        _addExporter(exporter);
    }

    public void RegisterSettingsPage(ISettingsPageDescriptor page)
    {
        if (!Has(CapabilitySettingsPage))
        {
            Logger.LogWarning("Plugin {Plugin} tried to register a settings page without capability", _pluginId);
            return;
        }
        _addSettingsPage(page);
    }

    public void RegisterStrings(IReadOnlyDictionary<string, string> strings)
    {
        if (!Has(CapabilityStrings))
        {
            Logger.LogWarning("Plugin {Plugin} tried to register strings without capability", _pluginId);
            return;
        }
        _addStrings(strings);
    }
}
