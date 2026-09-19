using System.Windows;
using System.Windows.Media;

namespace Viora.UI.Pages.PluginMarket;

/// <summary>
/// 市场插件条目(测试数据)。当前没有服务器,市场目录为内置测试插件;
/// 「安装/卸载」为本地模拟(标记持久化到 marketplace.json)。后续接入真实服务时,
/// 只需替换目录来源与安装实现,UI 结构不变。
/// </summary>
public sealed class MarketPlugin
{
    public required string Id { get; init; }

    public required string Name { get; init; }

    public required string Author { get; init; }

    public bool IsVerified { get; init; }

    public required string[] Tags { get; init; }

    /// <summary>分类键(Style.Category.Anime / Art / Realistic / Other)。</summary>
    public required string CategoryKey { get; init; }

    public double Rating { get; init; }

    public required string InstallsText { get; init; }

    public required string Version { get; init; }

    /// <summary>包大小(选中详情时由 OfficialPluginService 异步回填)。</summary>
    public required string SizeText { get; set; }

    /// <summary>包更新时间(选中详情时异步回填;镜像文件取 LastWriteTime,远程取 Last-Modified 头)。</summary>
    public required string UpdatedText { get; set; }

    public required string Description { get; init; }

    public bool IsRecommended { get; init; }

    /// <summary>更新日志(最新在前)。测试数据;真实插件由清单提供。</summary>
    public IReadOnlyList<ChangelogEntry> Changelog { get; init; } = Array.Empty<ChangelogEntry>();

    /// <summary>宿主插件 id(安装区文件夹名,如 builtin.viora.mosaic)。</summary>
    public string? PluginId { get; init; }

    /// <summary>官方预设 id(目录中预设的唯一键,如 builtin.mosaic)。</summary>
    public string? PresetId { get; init; }

    /// <summary>插件包在官方仓库内的相对路径(packages/<id>.zip)。</summary>
    public string PackageFile { get; init; } = "";

    /// <summary>插件仓库地址(作者在目录中自行配置;官方条目默认指向 packages 内的包)。</summary>
    public string? RepositoryUrl { get; init; }

    /// <summary>真实预览图(系统预设 = 内置样张;创作者插件未来由其上传)。</summary>
    public ImageSource? PreviewImage { get; init; }

    public Brush TileBrush { get; init; } = Brushes.Transparent;

    public Brush BeforeBrush { get; init; } = Brushes.Transparent;

    public Brush AfterBrush { get; init; } = Brushes.Transparent;

    /// <summary>确定性紫蓝渐变(与风格卡片同一套生成规则)。</summary>
    public static Brush MakeBrush(string seed, byte alphaLeft = 0xFF, byte alphaRight = 0xFF)
    {
        uint hash = 2166136261;
        foreach (char ch in seed) { hash ^= ch; hash *= 16777619; }
        double t = (hash % 1000) / 1000.0;
        var left = Color.FromArgb(alphaLeft, 0x8F, (byte)(0x7B + t * 0x10), (byte)(0xFF - t * 0x30));
        var right = Color.FromArgb(alphaRight, (byte)(0x5B + t * 0x10), (byte)(0x8C + t * 0x20), 0xE8);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(left, 0),
                new GradientStop(right, 1),
            },
        };
        brush.Freeze();
        return brush;
    }
}

/// <summary>一条更新日志。</summary>
public sealed record ChangelogEntry(string Version, string Date, string[] Items);

/// <summary>内置测试插件目录(暂代服务器数据,含 12 个测试插件)。</summary>
public static class MarketplaceCatalog
{
    public static IReadOnlyList<MarketPlugin> Plugins { get; } = Build();

    private static List<MarketPlugin> Build()
    {
        // 测试插件已移除:正式目录将由插件市场网站提供(GitHub 仓库链接 + manifest),
        // 接入后端时在此拉取并映射为 MarketPlugin。当前市场内容 = 「系统预设」(内置风格)。
        return new List<MarketPlugin>();
    }

    private static MarketPlugin Mock(
        string id, string name, string author, bool verified, string[] tags, string category,
        double rating, string installs, string version, string size, string updated, string desc)
    {
        // 测试日志:按当前版本生成前后两版(真实插件由清单提供)。
        var changelog = new List<ChangelogEntry>
        {
            new(version, updated, new[] { "提升边缘与细节质量", "优化处理速度与显存占用", "修复部分透明图片的问题" }),
            new(PrevVersion(version), "2025-06-10", new[] { "新增 3 组可调参数", "重构色彩映射管线" }),
        };
        return new MarketPlugin
        {
            Id = id,
            Name = name,
            Author = author,
            IsVerified = verified,
            Tags = tags,
            CategoryKey = category,
            Rating = rating,
            InstallsText = installs,
            Version = version,
            SizeText = size,
            UpdatedText = updated,
            Description = desc,
            Changelog = changelog,
            TileBrush = MarketPlugin.MakeBrush(id),
            BeforeBrush = MarketPlugin.MakeBrush(id + ".before"),
            AfterBrush = MarketPlugin.MakeBrush(id + ".after"),
        };
    }

    private static string PrevVersion(string version)
    {
        var parts = version.Split('.');
        if (parts.Length == 3 && int.TryParse(parts[1], out int minor) && minor > 0)
            return $"{parts[0]}.{minor - 1}.0";
        return "1.0.0";
    }
}
