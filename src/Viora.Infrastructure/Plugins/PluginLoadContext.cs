using System.IO;
using Microsoft.Extensions.Logging;
using Viora.Core.Plugins;
using Viora.PluginSdk;

namespace Viora.Infrastructure.Plugins;

/// <summary>
/// One loaded plugin: its collectible AssemblyLoadContext + instance.
/// Host code only; plugins interact solely via IPluginContext.
/// </summary>
public sealed class LoadedPlugin : IDisposable
{
    public required PluginManifest Manifest { get; init; }

    public required PluginMetadata Metadata { get; init; }

    public required PluginLoadContext LoadContext { get; init; }

    public IVioraPlugin? Instance { get; set; }

    public PluginLoadState State { get; set; } = PluginLoadState.Loaded;

    public string? ErrorMessage { get; set; }

    public string Folder { get; init; } = string.Empty;

    public void Dispose()
    {
        Instance = null;
        LoadContext.Unload();
    }
}

/// <summary>
/// Collectible ALC that resolves plugin-private assemblies from the plugin folder first,
/// then falls back to shared host assemblies (Core contracts, PluginSdk, BCL).
/// </summary>
public sealed class PluginLoadContext : System.Runtime.Loader.AssemblyLoadContext
{
    private readonly string _pluginFolder;

    public PluginLoadContext(string pluginFolder)
        : base($"viora-plugin:{Path.GetFileName(pluginFolder)}", isCollectible: true)
        => _pluginFolder = pluginFolder;

    protected override System.Reflection.Assembly? Load(System.Reflection.AssemblyName assemblyName)
    {
        // Private copy in the plugin folder wins (plugin dependency isolation).
        var candidate = Path.Combine(_pluginFolder, assemblyName.Name + ".dll");
        if (File.Exists(candidate))
            return LoadFromAssemblyPath(candidate);

        // Contracts are shared with the host so interfaces unify across the boundary.
        if (assemblyName.Name is "Viora.Core" or "Viora.PluginSdk"
            or "Microsoft.Extensions.Logging.Abstractions")
            return null; // fall back to the Default ALC (host's copy)

        return null;
    }
}
