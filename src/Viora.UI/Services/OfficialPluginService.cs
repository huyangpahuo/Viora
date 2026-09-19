using System.Net.Http;
using System.IO;
using System.IO.Compression;
using System.Text.Json;
using System.Windows;
using Microsoft.Extensions.Logging;
using Viora.Core;
using Viora.UI.Localization;

namespace Viora.UI.Services;

/// <summary>
/// 官方插件仓库服务:官方 GitHub 仓库(Viora-plugins)的目录镜像与安装包定位。
/// 目录来源优先级:内置快照(离线兜底)→ 本地镜像 → GitHub 拉取刷新。
/// 安装包定位:优先从 GitHub raw 下载(真实远程分发),网络不佳时回退本地镜像。
/// 客户端不预装任何插件:安装区从空开始,全部由用户在市场按需安装。
/// </summary>
public sealed class OfficialPluginService
{
    public const string RepoOwner = "huyangpahuo";
    public const string RepoName = "Viora-plugins";
    public const string RepoBranch = "main";

    /// <summary>用户插件安装区(exe 旁的 plugins 文件夹,与 AssemblyPluginHost 的发现目录一致)。</summary>
    public static readonly string PluginsDir = AppLocations.PluginsFolder;

    /// <summary>官方仓库条目。</summary>
    public sealed record OfficialItem(
        string PluginId, string PresetId, string NameZh, string NameEn,
        string Author, string Category, string Version, string DescZh, string DescEn,
        string PackageFile, string? Repository, string[] Tags);

    private static readonly HttpClient Http = CreateClient();

    private static HttpClient CreateClient()
    {
        var client = new HttpClient();
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Viora/1.0 (+https://github.com/huyangpahuo/Viora)");
        client.Timeout = TimeSpan.FromSeconds(6);
        return client;
    }

    private List<OfficialItem> _items = new();

    /// <summary>官方目录条目。</summary>
    public IReadOnlyList<OfficialItem> Items => _items;

    /// <summary>目录刷新后触发(UI 侧负责切线程)。</summary>
    public event EventHandler? Changed;

    private readonly ILogger<OfficialPluginService>? _logger;

    public OfficialPluginService(ILogger<OfficialPluginService>? logger = null)
    {
        _logger = logger;
        LoadBundledRegistry();
        _logger?.LogInformation("Official registry loaded: {Count} items", _items.Count);
    }

    // ---------- 目录 ----------

    public string NameOf((string NameZh, string NameEn) names) => LocalizationSource.Current.Language == "en" ? names.NameEn : names.NameZh;

    private void LoadBundledRegistry()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Viora.UI;component/Assets/official-registry.json"));
            if (info is not null)
            {
                using var reader = new StreamReader(info.Stream);
                ParseRegistry(reader.ReadToEnd());
            }
        }
        catch
        {
            // 快照缺失:走文件系统回退
        }

        if (_items.Count == 0)
        {
            // 回退:本地镜像的 registry.json(开发环境直接可用)
            foreach (var mirror in MirrorRoots())
            {
                var reg = Path.Combine(mirror, "registry.json");
                if (!File.Exists(reg)) continue;
                try { ParseRegistry(File.ReadAllText(reg)); break; }
                catch { /* 尝试下一个 */ }
            }
        }
    }

    private void ParseRegistry(string json)
    {
        using var doc = JsonDocument.Parse(json);
        // 兼容两种形状:{"presets":[...]} 对象 或裸 [...]
        var presetsEl = doc.RootElement.ValueKind == JsonValueKind.Array
            ? doc.RootElement
            : doc.RootElement.GetProperty("presets");
        var list = new List<OfficialItem>();
        foreach (var p in presetsEl.EnumerateArray())
        {
            string pluginId = p.GetProperty("pluginId").GetString() ?? string.Empty;
            if (pluginId.Length == 0) continue;
            string nameZh = p.GetProperty("name").GetProperty("zh").GetString() ?? pluginId;
            string nameEn = p.GetProperty("name").TryGetProperty("en", out var ne) ? ne.GetString() ?? nameZh : nameZh;
            string descZh = p.GetProperty("description").GetProperty("zh").GetString() ?? string.Empty;
            string descEn = p.GetProperty("description").TryGetProperty("en", out var de) ? de.GetString() ?? descZh : descZh;
            var tags = new List<string>();
            if (p.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                foreach (var t in tagsEl.EnumerateArray())
                    if (t.GetString() is { Length: > 0 } tag) tags.Add(tag);

            list.Add(new OfficialItem(
                pluginId,
                p.GetProperty("presetId").GetString() ?? string.Empty,
                nameZh, nameEn,
                p.GetProperty("author").GetString() ?? "Viora Team",
                p.GetProperty("category").GetString() ?? "Style.Cat.Experimental",
                p.GetProperty("version").GetString() ?? "1.0.0",
                descZh, descEn,
                p.GetProperty("package").GetString() ?? string.Empty,
                p.TryGetProperty("repository", out var repo) ? repo.GetString() : null,
                tags.ToArray()));
        }
        _items = list;
    }

    /// <summary>从官方 GitHub 仓库拉取最新 registry(网络不佳时静默保留当前目录)。</summary>
    public async Task RefreshFromGitHubAsync()
    {
        try
        {
            string url = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{RepoBranch}/registry.json";
            string json = await Http.GetStringAsync(url);
            ParseRegistry(json);
            RaiseChanged();
        }
        catch
        {
            // 网络不佳 / 仓库不可达:保留当前目录
        }
    }

    // ---------- 安装区 ----------

    public bool IsInstalled(string? pluginId) =>
        !string.IsNullOrEmpty(pluginId) && Directory.Exists(Path.Combine(PluginsDir, pluginId));

    /// <summary>
    /// 定位插件包:优先从官方 GitHub 仓库 raw 下载(真实的远程分发)到临时文件,
    /// 网络不可达时回退本地镜像 packages/(离线开发兜底)。返回 null = 两者均不可用。
    /// </summary>
    public async Task<string?> PreparePackageAsync(string packageFile)
    {
        try
        {
            string url = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{RepoBranch}/{packageFile}";
            string temp = Path.Combine(Path.GetTempPath(), packageFile.Replace('/', '_'));
            var bytes = await Http.GetByteArrayAsync(url);
            await File.WriteAllBytesAsync(temp, bytes);
            return temp;
        }
        catch
        {
            // 网络不佳 / 仓库不可达:回退本地镜像
        }

        foreach (var mirror in MirrorRoots())
        {
            var local = Path.Combine(mirror, packageFile.Replace('/', Path.DirectorySeparatorChar));
            if (File.Exists(local)) return local;
        }

        return null;
    }

    /// <summary>
    /// 插件包的展示元数据(大小 / 更新时间):本地镜像文件优先(离线可用),
    /// 否则向 GitHub raw 发起流式 GET,读取 Content-Length 与 Last-Modified 响应头。
    /// 失败返回 (null, null),UI 显示占位符。
    /// </summary>
    public async Task<(long? SizeBytes, DateTime? UpdatedUtc)> GetPackageInfoAsync(string packageFile)
    {
        foreach (var mirror in MirrorRoots())
        {
            var local = new FileInfo(Path.Combine(mirror, packageFile.Replace('/', Path.DirectorySeparatorChar)));
            if (local.Exists)
                return (local.Length, local.LastWriteTimeUtc);
        }

        try
        {
            string url = $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{RepoBranch}/{packageFile}";
            using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
            if (!response.IsSuccessStatusCode) return (null, null);
            long? size = response.Content.Headers.ContentLength;
            DateTime? updated = response.Content.Headers.LastModified?.UtcDateTime;
            return (size, updated);
        }
        catch
        {
            return (null, null);
        }
    }

    /// <summary>镜像根目录候选:exe 向上六层内的 Viora-plugins(开发态)与 %LOCALAPPDATA% 镜像。</summary>
    public static IEnumerable<string> MirrorRoots()
    {
        var baseDir = AppContext.BaseDirectory;
        var dir = Path.TrimEndingDirectorySeparator(Path.GetFullPath(baseDir));
        for (int i = 0; i < 6 && dir is not null; i++)
        {
            yield return Path.Combine(dir, RepoName);
            dir = Path.GetDirectoryName(dir);
        }
        yield return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Viora", "official_repo");
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
