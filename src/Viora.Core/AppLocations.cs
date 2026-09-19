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

    /// <summary>
    /// 应用数据根目录:exe 旁存在 portable.marker 时为 exe 目录(便携模式),
    /// 否则为 %LOCALAPPDATA%\Viora。与 Infrastructure.AppPaths 的判定保持一致。
    /// </summary>
    public static string DefaultRoot =>
        File.Exists(Path.Combine(AppContext.BaseDirectory, "portable.marker"))
            ? AppContext.BaseDirectory
            : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viora");

    /// <summary>解析日志目录:自定义优先,空 = 默认。</summary>
    public static string ResolveLogsFolder(string? overridePath) =>
        string.IsNullOrWhiteSpace(overridePath) ? Path.Combine(DefaultRoot, "logs") : overridePath;

    /// <summary>解析作品库目录:自定义优先,空 = 默认。</summary>
    public static string ResolveWorksFolder(string? overridePath) =>
        string.IsNullOrWhiteSpace(overridePath) ? Path.Combine(DefaultRoot, "works") : overridePath;
}
