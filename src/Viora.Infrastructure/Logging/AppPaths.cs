using System.IO;
using System.Security;
using Viora.Core;

namespace Viora.Infrastructure.Logging;

/// <summary>Resolves app-owned folder paths (settings, logs, cache, plugins).</summary>
public interface IAppPaths
{
    string Root { get; }

    string SettingsFile { get; }

    string LogsFolder { get; }

    string CacheFolder { get; }

    string PluginsFolder { get; }

    void EnsureDirectories();
}

/// <summary>
/// %LOCALAPPDATA%\Viora is the root for settings, logs and cache. When a
/// portable.marker file sits next to the executable, the executable folder is used
/// instead (portable installs). The plugin install area always sits next to the
/// executable so users can inspect and delete plugins in Explorer (see AppLocations).
/// </summary>
public sealed class AppPaths : IAppPaths
{
    private const string RedactionToken = "…";

    public AppPaths()
    {
        string portableRoot = Path.Combine(AppContext.BaseDirectory, "portable.marker");
        Root = File.Exists(portableRoot)
            ? AppContext.BaseDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viora");

        SettingsFile = Path.Combine(Root, "settings.json");
        LogsFolder = Path.Combine(Root, "logs");
        CacheFolder = Path.Combine(Root, "cache");
        PluginsFolder = AppLocations.PluginsFolder;
    }

    public string Root { get; }

    public string SettingsFile { get; }

    public string LogsFolder { get; }

    public string CacheFolder { get; }

    public string PluginsFolder { get; }

    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(LogsFolder);
        Directory.CreateDirectory(CacheFolder);
        Directory.CreateDirectory(PluginsFolder);
    }

    /// <summary>Redacts the user profile segment of a path for log privacy.</summary>
    public static string RedactPath(string path)
    {
        try
        {
            string profile = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            if (!string.IsNullOrEmpty(profile) && path.StartsWith(profile, StringComparison.OrdinalIgnoreCase))
                return RedactionToken + path[profile.Length..];
            return path;
        }
        catch (SecurityException)
        {
            return RedactionToken;
        }
    }
}
