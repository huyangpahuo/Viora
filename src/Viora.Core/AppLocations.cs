using System;
using System.IO;

namespace Viora.Core;

/// <summary>
/// App-owned folder locations shared across layers (single source of truth), so every
/// consumer (host discovery, market seeding, uninstall) resolves the same directories.
/// </summary>
public static class AppLocations
{
    /// <summary>
    /// Plugin install area: the plugins folder next to the executable by default, so
    /// users can inspect and delete plugins in Explorer. Falls back to
    /// %LOCALAPPDATA%\Viora\plugins when the executable folder is not writable
    /// (e.g. an install under Program Files).
    /// </summary>
    public static string PluginsFolder { get; } = ResolvePluginsFolder();

    private static string ResolvePluginsFolder()
    {
        var besideExe = Path.Combine(AppContext.BaseDirectory, "plugins");
        try
        {
            Directory.CreateDirectory(besideExe);
            return besideExe;
        }
        catch (Exception)
        {
            return Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "Viora", "plugins");
        }
    }
}
