using System.IO;
using Microsoft.Extensions.Logging;
using System.Text.Json;
using Viora.Infrastructure.Logging;
using Viora.UI.Theming;

namespace Viora.App.Hosting;

/// <summary>
/// Persists user-authored theme palettes as JSON files under %LOCALAPPDATA%\Viora\themes\.
/// </summary>
public sealed class UserThemeStore : IUserThemeStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly string _folder;
    private readonly ILogger<App> _logger;

    public UserThemeStore(IAppPaths paths, ILogger<App> logger)
    {
        _folder = Path.Combine(paths.Root, "themes");
        _logger = logger;
    }

    private string PathFor(string id) =>
        // 调用方可能传入 "user:xxx" 完整 id(ThemeManager.Apply 用它识别用户主题);
        // 冒号是 Windows 文件名非法字符,这里统一剥离前缀存储。
        Path.Combine(_folder, id.Replace("user:", string.Empty) + ".json");

    public IReadOnlyList<UserPalette> LoadAll()
    {
        if (!Directory.Exists(_folder)) return Array.Empty<UserPalette>();
        var list = new List<UserPalette>();
        foreach (var file in Directory.EnumerateFiles(_folder, "*.json"))
        {
            var palette = TryRead(file);
            if (palette is not null) list.Add(palette);
        }
        return list.OrderBy(p => p.Name, StringComparer.OrdinalIgnoreCase).ToList();
    }

    public UserPalette? Load(string id)
    {
        var file = PathFor(id);
        return File.Exists(file) ? TryRead(file) : null;
    }

    public void Save(UserPalette palette)
    {
        try
        {
            Directory.CreateDirectory(_folder);
            File.WriteAllText(PathFor(palette.Id), JsonSerializer.Serialize(palette, JsonOpts));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save user theme {Id}", palette.Id);
        }
    }

    public bool Delete(string id)
    {
        try
        {
            var file = PathFor(id);
            if (!File.Exists(file)) return false;
            File.Delete(file);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to delete user theme {Id}", id);
            return false;
        }
    }

    private static UserPalette? TryRead(string file)
    {
        try
        {
            return JsonSerializer.Deserialize<UserPalette>(File.ReadAllText(file), JsonOpts);
        }
        catch
        {
            return null; // corrupt palette files are skipped, not fatal
        }
    }
}
