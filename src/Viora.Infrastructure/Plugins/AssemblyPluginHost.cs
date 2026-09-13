using System.IO;
using System.IO.Compression;
using Microsoft.Extensions.Logging;
using Viora.Core.Localization;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Settings;
using Viora.Infrastructure.Logging; // IAppPaths
using Viora.PluginSdk;
using Viora.UI.Hosting;
using Viora.Infrastructure.Plugins;

namespace Viora.Infrastructure.Plugins;

/// <summary>
/// Filesystem plugin host: discovery reads plugin.json metadata only; load spins up a
/// collectible AssemblyLoadContext per plugin; enable/disable is independent of load;
/// all plugin code calls are contained at this boundary so a throwing plugin never
/// crashes the host.
/// </summary>
public sealed partial class AssemblyPluginHost : IPluginHost
{
    private readonly IAppPaths _paths;
    private readonly ISettingsService _settings;
    private readonly IPresetCatalog _catalog;
    private readonly ILocalizationService _localization;
    private readonly ILogger<AssemblyPluginHost> _logger;
    private readonly Dictionary<string, LoadedPlugin> _plugins = new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    public AssemblyPluginHost(
        IAppPaths paths,
        ISettingsService settings,
        IPresetCatalog catalog,
        ILocalizationService localization,
        ILogger<AssemblyPluginHost> logger)
    {
        _paths = paths;
        _settings = settings;
        _catalog = catalog;
        _localization = localization;
        _logger = logger;
        Context = new PluginContext(
            pluginId: null,
            capabilities: Array.Empty<string>(),
            logger: logger,
            addPreset: _catalog.Add,
            addExporter: _catalog.Add,
            addSettingsPage: _ => { /* settings pages from plugins land in Phase 5+ UI */ },
            addStrings: _localization.RegisterStrings);
    }

    public PluginContext Context { get; }

    public event EventHandler<PluginDescriptor>? PluginStateChanged;

    public Task<IReadOnlyList<PluginDescriptor>> DiscoverAsync(CancellationToken cancellationToken = default)
    {
        _paths.EnsureDirectories();
        foreach (var dir in Directory.EnumerateDirectories(_paths.PluginsFolder))
        {
            var manifestPath = Path.Combine(dir, "plugin.json");
            if (!File.Exists(manifestPath)) continue;

            var manifest = PluginManifest.TryLoad(manifestPath);
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id))
            {
                LogInvalidManifest(_logger, dir);
                continue;
            }

            lock (_gate)
            {
                if (_plugins.ContainsKey(manifest.Id)) continue;

                var metadata = ToMetadata(manifest);
                var state = !metadata.RequiredHostVersion.Contains(HostVersion.Current)
                    ? PluginLoadState.Incompatible
                    : _settings.Current.Plugins.DisabledPlugins.Contains(manifest.Id)
                        ? PluginLoadState.Discovered
                        : PluginLoadState.Discovered;
                _plugins[manifest.Id] = new LoadedPlugin
                {
                    Manifest = manifest,
                    Metadata = metadata,
                    LoadContext = new PluginLoadContext(dir),
                    State = state,
                    Folder = dir,
                };
            }
        }

        return Task.FromResult(GetPluginsSnapshot());
    }

    public async Task<PluginLoadResult> LoadAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        LoadedPlugin? plugin;
        lock (_gate)
        {
            if (!_plugins.TryGetValue(pluginId, out plugin))
                return new PluginLoadResult(false, pluginId, "Plugin not found.");
            if (plugin.State == PluginLoadState.Enabled || plugin.State == PluginLoadState.Loaded)
                return new PluginLoadResult(true, pluginId, null);
        }

        if (plugin.State == PluginLoadState.Incompatible)
        {
            var msg = $"Requires host {plugin.Metadata.RequiredHostVersion.Raw}";
            plugin.ErrorMessage = msg;
            return new PluginLoadResult(false, pluginId, msg);
        }

        if (!_settings.Current.Plugins.EnablePluginLoading)
            return new PluginLoadResult(false, pluginId, "Plugin loading is disabled in settings.");

        try
        {
            var assemblyPath = Path.Combine(plugin.Folder, plugin.Manifest.EntryAssembly);
            var assembly = plugin.LoadContext.LoadFromAssemblyPath(assemblyPath);
            var type = assembly.GetType(plugin.Manifest.TypeName)
                ?? throw new TypeLoadException($"Type '{plugin.Manifest.TypeName}' not found.");
            var instance = (IVioraPlugin)(Activator.CreateInstance(type)
                ?? throw new InvalidOperationException("Plugin constructor returned null."));

            plugin.Instance = instance;
            await instance.InitializeAsync(
                ScopeContext(plugin.Manifest.Id, plugin.Manifest.Capabilities), cancellationToken);

            plugin.State = PluginLoadState.Loaded;
            LogLoaded(_logger, pluginId);
            RaiseChanged(plugin);
            return new PluginLoadResult(true, pluginId, null);
        }
#pragma warning disable CA1031 // Error containment boundary: plugin faults must not crash the host.
        catch (Exception ex)
        {
            plugin.State = PluginLoadState.Failed;
            plugin.ErrorMessage = ex.Message;
            LogLoadFailed(_logger, pluginId, ex);
            RaiseChanged(plugin);
            return new PluginLoadResult(false, pluginId, ex.Message);
        }
#pragma warning restore CA1031
    }

    public Task UnloadAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        lock (_gate)
        {
            if (_plugins.TryGetValue(pluginId, out var plugin) && plugin.State is PluginLoadState.Loaded or PluginLoadState.Enabled)
            {
                try
                {
                    plugin.Instance?.ShutdownAsync(cancellationToken).Wait(cancellationToken);
                }
                catch (Exception ex)
                {
                    LogShutdownFailed(_logger, pluginId, ex);
                }

                bool unloaded = TryUnload(plugin);
                plugin.State = PluginLoadState.Discovered;
                if (!unloaded)
                    LogUnloadDeferred(_logger, pluginId); // keep disabled-but-loaded trade-off documented
                RaiseChanged(plugin);
            }
        }
        return Task.CompletedTask;
    }

    public async Task EnableAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        _settings.Update(s => s.Plugins.DisabledPlugins.Remove(pluginId));
        await LoadAsync(pluginId, cancellationToken);
        lock (_gate)
        {
            if (_plugins.TryGetValue(pluginId, out var p) && p.State == PluginLoadState.Loaded)
            {
                p.State = PluginLoadState.Enabled;
                RaiseChanged(p);
            }
        }
    }

    public async Task DisableAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        _settings.Update(s => { if (!s.Plugins.DisabledPlugins.Contains(pluginId)) s.Plugins.DisabledPlugins.Add(pluginId); });
        await UnloadAsync(pluginId, cancellationToken);
        lock (_gate)
        {
            if (_plugins.TryGetValue(pluginId, out var p))
            {
                p.State = PluginLoadState.Disabled;
                RaiseChanged(p);
            }
        }
    }

    public async Task<InstallResult> InstallFromPackageAsync(string packagePath, CancellationToken cancellationToken = default)
    {
        try
        {
            _paths.EnsureDirectories();
            using var archive = ZipFile.OpenRead(packagePath);
            var manifestEntry = archive.Entries.FirstOrDefault(e =>
                e.FullName.Equals("plugin.json", StringComparison.OrdinalIgnoreCase));
            if (manifestEntry is null)
                return new InstallResult(false, "invalid_package", "plugin.json missing.");

            using var reader = new StreamReader(manifestEntry.Open());
            var manifest = JsonSerializerSafeParse(await reader.ReadToEndAsync());
            if (manifest is null || string.IsNullOrWhiteSpace(manifest.Id) || manifest.EntryAssembly.Length == 0)
                return new InstallResult(false, "invalid_package", "Manifest incomplete.");

            var target = Path.Combine(_paths.PluginsFolder, manifest.Id);
            if (Directory.Exists(target))
                return new InstallResult(false, "duplicate", "Already installed.");

            Directory.CreateDirectory(target);
            archive.ExtractToDirectory(target, overwriteFiles: true);

            LogInstalled(_logger, manifest.Id);
            return new InstallResult(true, null, manifest.Id);
        }
        catch (Exception ex)
        {
            LogInstallFailed(_logger, ex);
            return new InstallResult(false, "error", ex.Message);
        }
    }

    public Task UninstallAsync(string pluginId, CancellationToken cancellationToken = default)
    {
        LoadedPlugin? plugin;
        lock (_gate)
        {
            if (!_plugins.TryGetValue(pluginId, out plugin)) return Task.CompletedTask;
            _plugins.Remove(pluginId);
        }

        if (plugin.State is PluginLoadState.Loaded or PluginLoadState.Enabled)
        {
            try { plugin.Instance?.ShutdownAsync(cancellationToken).Wait(cancellationToken); }
            catch { /* containment: uninstall must proceed */ }
            TryUnload(plugin);
        }

        try
        {
            if (Directory.Exists(plugin.Folder)) Directory.Delete(plugin.Folder, recursive: true);
            LogUninstalled(_logger, pluginId);
        }
        catch (IOException ex)
        {
            LogUninstallIoError(_logger, pluginId, ex);
        }

        plugin.State = PluginLoadState.Disabled;
        PluginStateChanged?.Invoke(this, ToDescriptor(plugin));
        return Task.CompletedTask;
    }

    public Task<IReadOnlyList<PluginDescriptor>> GetPluginsAsync(CancellationToken cancellationToken = default)
        => Task.FromResult(GetPluginsSnapshot());

    private IReadOnlyList<PluginDescriptor> GetPluginsSnapshot()
    {
        lock (_gate)
            return _plugins.Values
                .OrderBy(p => p.Metadata.DisplayName, StringComparer.OrdinalIgnoreCase)
                .Select(ToDescriptor)
                .ToList();
    }

    private PluginDescriptor ToDescriptor(LoadedPlugin p) => new(
        p.Metadata,
        p.State,
        p.ErrorMessage,
        IsBuiltin: false);

    private void RaiseChanged(LoadedPlugin p) => PluginStateChanged?.Invoke(this, ToDescriptor(p));

    private PluginContext ScopeContext(string pluginId, IReadOnlyList<string> capabilities) => new(
        pluginId,
        capabilities,
        _logger,
        _catalog.Add,
        _catalog.Add,
        _ => { },
        _localization.RegisterStrings);

    private static bool TryUnload(LoadedPlugin plugin)
    {
        try
        {
            plugin.Dispose();
            return true;
        }
        catch (InvalidOperationException)
        {
            return false; // collectible ALC still referenced; stays memory-resident but disabled
        }
    }

    private static PluginMetadata ToMetadata(PluginManifest m) => new(
        m.Id,
        m.DisplayName,
        Version.Parse(m.Version),
        m.Author,
        m.Description,
        Uri.TryCreate(m.Homepage, UriKind.Absolute, out var home) ? home : null,
        Uri.TryCreate(m.Repository, UriKind.Absolute, out var repo) ? repo : null,
        VersionRange.Parse(m.RequiredHostVersion),
        m.Capabilities,
        m.Dependencies.Select(d => new PluginDependency(d.PluginId, VersionRange.Parse(d.VersionRange))).ToList());

    private static PluginManifest? JsonSerializerSafeParse(string json)
    {
        try { return System.Text.Json.JsonSerializer.Deserialize<PluginManifest>(json, new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true }); }
        catch { return null; }
    }

    [LoggerMessage(EventId = 1, Level = LogLevel.Warning, Message = "Plugin folder {Folder} has an invalid plugin.json; skipped")]
    private static partial void LogInvalidManifest(ILogger logger, string folder);

    [LoggerMessage(EventId = 2, Level = LogLevel.Information, Message = "Plugin {PluginId} loaded")]
    private static partial void LogLoaded(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 3, Level = LogLevel.Error, Message = "Plugin {PluginId} failed to load")]
    private static partial void LogLoadFailed(ILogger logger, string pluginId, Exception ex);

    [LoggerMessage(EventId = 4, Level = LogLevel.Warning, Message = "Plugin {PluginId} ShutdownAsync threw; unloading anyway")]
    private static partial void LogShutdownFailed(ILogger logger, string pluginId, Exception ex);

    [LoggerMessage(EventId = 5, Level = LogLevel.Information,
        Message = "Plugin {PluginId} could not fully unload (live references); disabled but memory-resident")]
    private static partial void LogUnloadDeferred(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 6, Level = LogLevel.Information, Message = "Plugin package {PluginId} installed")]
    private static partial void LogInstalled(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 7, Level = LogLevel.Error, Message = "Plugin package installation failed")]
    private static partial void LogInstallFailed(ILogger logger, Exception ex);

    [LoggerMessage(EventId = 8, Level = LogLevel.Information, Message = "Plugin {PluginId} uninstalled")]
    private static partial void LogUninstalled(ILogger logger, string pluginId);

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Plugin {PluginId} folder could not be deleted (locked); it will vanish on next start")]
    private static partial void LogUninstallIoError(ILogger logger, string pluginId, Exception ex);
}
