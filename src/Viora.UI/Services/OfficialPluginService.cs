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
/// 官方插件仓库服务:官方 GitHub 仓库(Viora-plugins)与官网(viora-web)社区目录的
/// 镜像与安装包定位。
/// 目录来源优先级:磁盘镜像(最近一次联网成功的结果)→ 内置快照(编译期离线兜底)→ 在线刷新。
/// 官方目录来自 GitHub registry.json;社区目录来自官网数据库(作者发布,安装包托管在
/// 作者自己的 GitHub 仓库,package 是绝对直链)。两条并列管道,失败互不影响。
/// 客户端不预装任何插件:安装区从空开始,全部由用户在市场按需安装。
/// </summary>
public sealed class OfficialPluginService
{
    public const string RepoOwner = "huyangpahuo";
    public const string RepoName = "Viora-plugins";
    public const string RepoBranch = "main";

    /// <summary>官网(viora-web)站点根地址;社区插件目录 API 挂在其下。</summary>
    public const string WebsiteBaseUrl = "https://viora-web.vercel.app";

    /// <summary>官网社区插件目录 API(registry.json 兼容形状;package 为作者仓库绝对直链)。</summary>
    public static string WebsiteCatalogUrl => $"{WebsiteBaseUrl}/api/plugins";

    /// <summary>用户插件安装区(exe 旁的 plugins 文件夹,与 AssemblyPluginHost 的发现目录一致)。</summary>
    public static readonly string PluginsDir = AppLocations.PluginsFolder;

    /// <summary>官方仓库条目。PresetId 仅官方条目携带;社区条目安装后按 PluginId 识别。</summary>
    public sealed record OfficialItem(
        string PluginId, string? PresetId, string NameZh, string NameEn,
        string Author, string Category, string Version, string DescZh, string DescEn,
        string PackageFile, string? Repository, string[] Tags);

    // 目录刷新也要拉完整 registry;raw 在部分地区实测超过 8s,超时上限过紧会让第一源永远失败。
    private static readonly HttpClient Http = CreateClient(TimeSpan.FromSeconds(20));

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

    /// <summary>
    /// 安装包候选下载地址。官方条目的 package 是官方仓库内相对路径(raw → jsDelivr 两个源);
    /// 社区条目的 package 是作者仓库内的绝对直链 —— raw 直链附加 jsDelivr 镜像提升可达性。
    /// </summary>
    private static IEnumerable<string> PackageUrls(string packageFile)
    {
        if (!packageFile.StartsWith("http", StringComparison.OrdinalIgnoreCase))
        {
            foreach (var url in RemoteUrls(packageFile)) yield return url;
            yield break;
        }

        yield return packageFile;
        const string RawPrefix = "https://raw.githubusercontent.com/";
        if (packageFile.StartsWith(RawPrefix, StringComparison.OrdinalIgnoreCase))
        {
            // raw.githubusercontent.com/<owner>/<repo>/<branch>/<path> → cdn.jsdelivr.net 镜像
            var parts = packageFile[RawPrefix.Length..].Split('/', 4);
            if (parts.Length == 4)
                yield return $"https://cdn.jsdelivr.net/gh/{parts[0]}/{parts[1]}@{parts[2]}/{parts[3]}";
        }
    }

    /// <summary>下载临时文件名:绝对直链含冒号等非法文件名字符,以 URL 稳定哈希 + 尾段命名避免冲突。</summary>
    private static string TempFileName(string packageFile)
    {
        string name = packageFile;
        if (Uri.TryCreate(packageFile, UriKind.Absolute, out var uri) && uri.Segments.Length > 0)
            name = uri.Segments[^1];
        var safe = new string(name.Where(c => char.IsLetterOrDigit(c) || c is '.' or '-' or '_').Take(80).ToArray());
        var hash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(packageFile)))[..12];
        return $"{hash}-{(safe.Length > 0 ? safe : "package.zip")}";
    }

    private List<OfficialItem> _officialItems = new();
    private List<OfficialItem> _communityItems = new();

    /// <summary>目录条目:官方(GitHub registry)+ 社区(官网数据库)两条管道的并集。</summary>
    public IReadOnlyList<OfficialItem> Items => _officialItems.Concat(_communityItems).ToList();

    /// <summary>目录刷新后触发(UI 侧负责切线程)。</summary>
    public event EventHandler? Changed;

    private readonly ILogger<OfficialPluginService>? _logger;

    public OfficialPluginService(ILogger<OfficialPluginService>? logger = null)
    {
        _logger = logger;
        LoadBundledRegistry();
        LoadCachedRegistries();
        _logger?.LogInformation("Official registry loaded: {Official} official + {Community} community items",
            _officialItems.Count, _communityItems.Count);
    }

    /// <summary>在线目录的磁盘镜像:最近一次成功拉取的结果,比编译期快照新;离线会话也不会退回过期数据。</summary>
    private static string CacheFilePath => Path.Combine(AppLocations.CacheFolder, "official-registry.json");

    private static string CommunityCacheFilePath => Path.Combine(AppLocations.CacheFolder, "community-registry.json");

    private void LoadCachedRegistries()
    {
        TryLoadCache(CacheFilePath, official: true);
        TryLoadCache(CommunityCacheFilePath, official: false);
    }

    private void TryLoadCache(string path, bool official)
    {
        try
        {
            if (!File.Exists(path)) return;
            var items = ParseRegistry(File.ReadAllText(path));
            if (official) _officialItems = items;
            else _communityItems = items;
        }
        catch
        {
            // 镜像损坏:保留内置快照
        }
    }

    private void SaveCache(string path, string json)
    {
        try
        {
            Directory.CreateDirectory(AppLocations.CacheFolder);
            File.WriteAllText(path, json);
        }
        catch
        {
            // 镜像写入失败不影响本次会话
        }
    }

    // ---------- 目录 ----------

    /// <summary>加载编译进程序的目录快照(离线兜底);在线目录由刷新方法更新。</summary>
    private void LoadBundledRegistry()
    {
        try
        {
            var info = Application.GetResourceStream(new Uri("pack://application:,,,/Viora.UI;component/Assets/official-registry.json"));
            if (info is not null)
            {
                using var reader = new StreamReader(info.Stream);
                _officialItems = ParseRegistry(reader.ReadToEnd());
            }
        }
        catch
        {
            // 快照缺失:目录为空,联网刷新后恢复
        }
    }

    /// <summary>
    /// 解析 registry 形状的目录 JSON,返回条目列表。
    /// 官方(GitHub registry.json)与社区(官网 /api/plugins)共用此形状;presetId 与
    /// description 对社区条目可缺失,package 可以是官方仓库相对路径或作者仓库绝对直链。
    /// </summary>
    private List<OfficialItem> ParseRegistry(string json)
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
            string descZh = string.Empty, descEn = string.Empty;
            if (p.TryGetProperty("description", out var de) && de.ValueKind == JsonValueKind.Object)
            {
                descZh = de.TryGetProperty("zh", out var dz) ? dz.GetString() ?? string.Empty : string.Empty;
                descEn = de.TryGetProperty("en", out var dn) ? dn.GetString() ?? descZh : descZh;
            }
            var tags = new List<string>();
            if (p.TryGetProperty("tags", out var tagsEl) && tagsEl.ValueKind == JsonValueKind.Array)
                foreach (var t in tagsEl.EnumerateArray())
                    if (t.GetString() is { Length: > 0 } tag) tags.Add(tag);

            list.Add(new OfficialItem(
                pluginId,
                p.TryGetProperty("presetId", out var pe) ? pe.GetString() : null,
                nameZh, nameEn,
                p.TryGetProperty("author", out var au) ? au.GetString() ?? "Viora Team" : "Viora Team",
                p.TryGetProperty("category", out var ca) ? ca.GetString() ?? "Style.Cat.Experimental" : "Style.Cat.Experimental",
                p.TryGetProperty("version", out var ve) ? ve.GetString() ?? "1.0.0" : "1.0.0",
                descZh, descEn,
                p.TryGetProperty("package", out var pk) ? pk.GetString() ?? string.Empty : string.Empty,
                p.TryGetProperty("repository", out var repo) ? repo.GetString() : null,
                tags.ToArray()));
        }
        return list;
    }

    /// <summary>从官方 GitHub 仓库拉取最新 registry(raw → jsDelivr;全部失败时保留当前目录并记日志)。</summary>
    public async Task RefreshFromGitHubAsync()
    {
        foreach (var url in RemoteUrls("registry.json"))
        {
            try
            {
                string json = await Http.GetStringAsync(url);
                _officialItems = ParseRegistry(json);
                SaveCache(CacheFilePath, json);
                RaiseChanged();
                return;
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "Registry refresh failed for {Url}, trying next source", url);
            }
        }

        _logger?.LogWarning("Registry refresh failed on all sources; keeping current catalog ({Count} items)", _officialItems.Count);
    }

    /// <summary>
    /// 从官网拉取社区插件目录(作者在 viora-web 发布、记录在数据库的条目)。
    /// 失败只影响社区管道,官方目录照常工作。
    /// </summary>
    public async Task RefreshCommunityAsync()
    {
        try
        {
            string json = await Http.GetStringAsync(WebsiteCatalogUrl);
            _communityItems = ParseRegistry(json);
            SaveCache(CommunityCacheFilePath, json);
            RaiseChanged();
        }
        catch (Exception ex)
        {
            _logger?.LogWarning(ex, "Community catalog refresh failed; keeping current community list ({Count} items)", _communityItems.Count);
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
    /// 定位插件包:官方条目走官方仓库 raw → jsDelivr;社区条目走作者仓库绝对直链(附镜像)。
    /// 下载到临时文件;全部不可用返回 null。
    /// </summary>
    public async Task<string?> PreparePackageAsync(string packageFile)
    {
        string temp = Path.Combine(Path.GetTempPath(), TempFileName(packageFile));
        foreach (var url in PackageUrls(packageFile))
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
        foreach (var url in PackageUrls(packageFile))
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
