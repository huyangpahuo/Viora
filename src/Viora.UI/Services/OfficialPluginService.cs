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

    private static readonly HttpClient Http = CreateClient(TimeSpan.FromSeconds(8));

    /// <summary>包下载专用:整体超时放宽到 120s(原 6s 全局超时让稍大的包在正常网络也装不上)。</summary>
    private static readonly HttpClient DownloadHttp = CreateClient(TimeSpan.FromMinutes(2));

    private static HttpClient CreateClient(TimeSpan timeout)
    {
        var client = new HttpClient { Timeout = timeout };
        client.DefaultRequestHeaders.UserAgent.ParseAdd("Viora/1.0 (+https://github.com/huyangpahuo/Viora)");
        return client;
    }

    /// <summary>同一文件的候选下载源:GitHub raw 优先,jsDelivr CDN 兜底(raw 在部分地区不可达)。</summary>
    private static IEnumerable<string> RemoteUrls(string repoPath)
    {
        yield return $"https://raw.githubusercontent.com/{RepoOwner}/{RepoName}/{RepoBranch}/{repoPath}";
        yield return $"https://cdn.jsdelivr.net/gh/{RepoOwner}/{RepoName}@{RepoBranch}/{repoPath}";
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

    /// <summary>加载编译进程序的目录快照(离线兜底);在线目录由 RefreshFromGitHubAsync 刷新。</summary>
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
            // 快照缺失:目录为空,联网刷新后恢复
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

    /// <summary>从官方 GitHub 仓库拉取最新 registry(raw → jsDelivr;网络不佳时静默保留当前目录)。</summary>
    public async Task RefreshFromGitHubAsync()
    {
        foreach (var url in RemoteUrls("registry.json"))
        {
            try
            {
                string json = await Http.GetStringAsync(url);
                ParseRegistry(json);
                RaiseChanged();
                return;
            }
            catch
            {
                // 尝试下一个源
            }
        }
    }

    // ---------- 安装区 ----------

    public bool IsInstalled(string? pluginId) =>
        !string.IsNullOrEmpty(pluginId) && Directory.Exists(Path.Combine(PluginsDir, pluginId));

    /// <summary>已安装插件的清单版本(读安装区 plugin.json);未安装或清单损坏返回 null。</summary>
    public string? GetInstalledVersion(string? pluginId)
    {
        if (string.IsNullOrEmpty(pluginId)) return null;
        try
        {
            var path = Path.Combine(PluginsDir, pluginId, "plugin.json");
            if (!File.Exists(path)) return null;
            using var doc = JsonDocument.Parse(File.ReadAllText(path));
            var version = doc.RootElement.GetProperty("version").GetString();
            return string.IsNullOrWhiteSpace(version) ? null : version.Trim();
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 定位插件包:GitHub raw → jsDelivr CDN,下载到临时文件。全部不可用返回 null。
    /// </summary>
    public async Task<string?> PreparePackageAsync(string packageFile)
    {
        string temp = Path.Combine(Path.GetTempPath(), packageFile.Replace('/', '_'));
        foreach (var url in RemoteUrls(packageFile))
        {
            try
            {
                var bytes = await DownloadHttp.GetByteArrayAsync(url);
                await File.WriteAllBytesAsync(temp, bytes);
                return temp;
            }
            catch
            {
                // 尝试下一个源
            }
        }

        return null;
    }

    /// <summary>
    /// 插件包的展示元数据(大小 / 更新时间):向下载源发起流式 GET,
    /// 读取 Content-Length 与 Last-Modified 响应头。失败返回 (null, null),UI 显示占位符。
    /// </summary>
    public async Task<(long? SizeBytes, DateTime? UpdatedUtc)> GetPackageInfoAsync(string packageFile)
    {
        foreach (var url in RemoteUrls(packageFile))
        {
            try
            {
                using var response = await Http.GetAsync(url, HttpCompletionOption.ResponseHeadersRead);
                if (!response.IsSuccessStatusCode) continue;
                long? size = response.Content.Headers.ContentLength;
                DateTime? updated = response.Content.Headers.LastModified?.UtcDateTime;
                return (size, updated);
            }
            catch
            {
                // 尝试下一个源
            }
        }

        return (null, null);
    }

    private void RaiseChanged() => Changed?.Invoke(this, EventArgs.Empty);
}
