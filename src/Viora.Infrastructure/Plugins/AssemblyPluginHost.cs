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
        SweepQuarantinedFolders();
        foreach (var dir in Directory.EnumerateDirectories(_paths.PluginsFolder))
        {
            if (Path.GetFileName(dir).Contains(QuarantineSuffix, StringComparison.Ordinal)) continue;
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
            // 可收集 ALC 的卸载在 GC 后才真正完成;主动催收一次,让目录大概率能当场删除。
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        DeletePluginFolder(pluginId, plugin.Folder);

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

    /// <summary>隔离目录后缀:卸载时仍被占用的插件目录改名暂存,下次启动清扫。</summary>
    private const string QuarantineSuffix = ".uninstalling-";

    /// <summary>
    /// 删除插件目录。插件刚卸载时其 DLL 可能仍被加载器映射(可收集 ALC 的卸载要等
    /// GC 完成),Windows 不允许删除被映射的 DLL;此时把整个目录改名为隔离目录交给
    /// 下次启动清扫,卸载流程本身不失败。
    /// </summary>
    private void DeletePluginFolder(string pluginId, string folder)
    {
        if (!Directory.Exists(folder)) return;

        try
        {
            Directory.Delete(folder, recursive: true);
            LogUninstalled(_logger, pluginId);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            try
            {
                var quarantine = folder + QuarantineSuffix + DateTime.UtcNow.ToString("yyyyMMddHHmmss");
                Directory.Move(folder, quarantine);
                LogUninstallDeferred(_logger, pluginId, Path.GetFileName(quarantine));
            }
            catch (Exception moveEx)
            {
                LogUninstallIoError(_logger, pluginId, moveEx);
            }
        }
    }

    /// <summary>清掉上次卸载时仍被占用而隔离的插件目录(此时未加载,可直接删除)。</summary>
    private void SweepQuarantinedFolders()
    {
        try
        {
            foreach (var dir in Directory.EnumerateDirectories(_paths.PluginsFolder, "*" + QuarantineSuffix + "*"))
            {
                try
                {
                    Directory.Delete(dir, recursive: true);
                    LogQuarantineSwept(_logger, Path.GetFileName(dir));
                }
                catch (Exception ex)
                {
                    LogQuarantineSweepFailed(_logger, Path.GetFileName(dir), ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogQuarantineSweepFailed(_logger, _paths.PluginsFolder, ex);
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

    [LoggerMessage(EventId = 9, Level = LogLevel.Warning, Message = "Plugin {PluginId} folder could not be deleted or quarantined (locked); removal will be retried on next start")]
    private static partial void LogUninstallIoError(ILogger logger, string pluginId, Exception ex);

    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "Plugin {PluginId} files still locked; folder quarantined as {QuarantinedFolder}, removed on next start")]
    private static partial void LogUninstallDeferred(ILogger logger, string pluginId, string quarantinedFolder);

    [LoggerMessage(EventId = 11, Level = LogLevel.Information, Message = "Removed quarantined plugin folder {Folder}")]
    private static partial void LogQuarantineSwept(ILogger logger, string folder);

    [LoggerMessage(EventId = 12, Level = LogLevel.Warning, Message = "Could not remove quarantined plugin folder {Folder}; will retry on next start")]
    private static partial void LogQuarantineSweepFailed(ILogger logger, string folder, Exception ex);
}
