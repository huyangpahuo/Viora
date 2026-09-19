using System.Collections.ObjectModel;
using System.Windows.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Viora.Core.Plugins;
using Viora.UI.Hosting;
using Viora.UI.Localization;
using Viora.UI.Services;

namespace Viora.UI.Pages.PluginMarket;

/// <summary>市场卡片视图模型(= 右侧详情面板数据源)。</summary>
public sealed partial class MarketCardViewModel : ObservableObject
{
    public MarketCardViewModel(PluginMarketViewModel owner, MarketPlugin plugin, bool isInstalled)
    {
        Owner = owner;
        Model = plugin;
        _isInstalled = isInstalled;
    }

    public PluginMarketViewModel Owner { get; }

    public MarketPlugin Model { get; }

    public string Name => Model.Name;

    public string Author => Model.Author;

    public bool IsVerified => Model.IsVerified;

    public string RatingText => Model.Rating.ToString("0.0");

    public string InstallsDisplay => string.Format(Tr.Get("Market.Rating.Installs"), Model.InstallsText);

    /// <summary>详情面板评分:「4.9 (12.6k 评价)」。</summary>
    public string RatingDetailText =>
        $"{Model.Rating:0.0} ({string.Format(Tr.Get("Market.Rating.Reviews"), Model.InstallsText)})";

    public string AuthorTooltip => string.Format(Tr.Get("Market.Author.Tooltip"), Model.Author);

    public bool HasRepository => !string.IsNullOrEmpty(Model.RepositoryUrl);

    /// <summary>点击作者名 → 打开插件的 GitHub 仓库(地址由作者在目录中配置,官方条目指向 packages 内的包)。</summary>
    [RelayCommand]
    private void OpenRepository()
    {
        if (Model.RepositoryUrl is not { } url) return;
        try
        {
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(url) { UseShellExecute = true });
        }
        catch
        {
            // 没有默认浏览器等极端情况:静默即可,不影响市场使用
        }
    }

    public string CategoryDisplay => Tr.Get(Model.CategoryKey);

    public string TagsDisplay => string.Join(" / ", Model.Tags);

    public string MetaLine =>
        $"{Tr.Get("Market.Detail.Version")}: {Model.Version}      {Tr.Get("Market.Detail.Size")}: {Model.SizeText}      {Tr.Get("Market.Detail.Updated")}: {Model.UpdatedText}";

    public bool HasPreviewImage => Model.PreviewImage is not null;

    [ObservableProperty]
    private bool _isInstalled;

    /// <summary>本卡片是否正在安装(按钮显示安装中)。</summary>
    [ObservableProperty]
    private bool _isInstallingThis;

    public bool ShowInstallButton => !IsInstalled;

    public bool CanInstall => !IsInstalled && !Owner.IsInstalling;

    partial void OnIsInstalledChanged(bool value)
    {
        OnPropertyChanged(nameof(ShowInstallButton));
        OnPropertyChanged(nameof(CanInstall));
    }

    /// <summary>语言切换后刷新文案(主 VM 通过它触发,避免跨类调用受保护成员)。</summary>
    public void RefreshMeta()
    {
        OnPropertyChanged(nameof(InstallsDisplay));
        OnPropertyChanged(nameof(RatingDetailText));
        OnPropertyChanged(nameof(MetaLine));
        OnPropertyChanged(nameof(CategoryDisplay));
    }

    /// <summary>安装状态变化后由主 VM 调用。</summary>
    public void NotifyInstallStateChanged() => OnPropertyChanged(nameof(IsInstallingThis));

    [RelayCommand]
    private Task Install() => Owner.InstallAsync(this);

    [RelayCommand]
    private Task UninstallAsync() => Owner.UninstallAsync(this);

    [RelayCommand]
    private void Changelog() => Owner.ShowChangelog(this);
}

public partial class PluginMarketViewModel : ObservableObject
{
    private readonly IUiAlert _alert;
    private readonly ILogger<PluginMarketViewModel> _logger;
    private readonly IPresetCatalog _catalog;
    private readonly IPluginHost _pluginHost;
    private readonly OfficialPluginService _official;

    /// <summary>内置样张(官方预设展示图;创作者插件未来上传自己的截图,加载失败时回退默认渐变)。</summary>
    private static readonly ImageSource SampleImage = LoadSampleImage();

    private static ImageSource? LoadSampleImage()
    {
        try
        {
            var image = new System.Windows.Media.Imaging.BitmapImage();
            image.BeginInit();
            image.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 800;
            image.UriSource = new Uri("pack://application:,,,/Viora.UI;component/sample.jpg");
            image.EndInit();
            image.Freeze();
            return image;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// 效果预览:封面图自动切分的左半(风格化前)与右半(风格化后)。
    /// Viora Team 的插件统一使用 sample.jpg,后续替换真实截图即可;社区插件
    /// 之后提供自己的封面图时走同一逻辑。
    /// </summary>
    private static readonly Brush SampleBeforeBrush = MakeHalfBrush(SampleImage, left: true);
    private static readonly Brush SampleAfterBrush = MakeHalfBrush(SampleImage, left: false);

    private static Brush MakeHalfBrush(ImageSource? source, bool left)
    {
        if (source is not System.Windows.Media.Imaging.BitmapImage bmp
            || bmp.PixelWidth < 2 || bmp.PixelHeight == 0)
            return MarketPlugin.MakeBrush(left ? "preview.before" : "preview.after");

        int halfWidth = bmp.PixelWidth / 2;
        var half = new System.Windows.Media.Imaging.CroppedBitmap(
            bmp,
            new System.Windows.Int32Rect(left ? 0 : bmp.PixelWidth - halfWidth, 0, halfWidth, bmp.PixelHeight));
        half.Freeze();
        var brush = new ImageBrush(half) { Stretch = Stretch.UniformToFill };
        brush.Freeze();
        return brush;
    }

    private bool _installing;

    public PluginMarketViewModel(IUiAlert alert, ILogger<PluginMarketViewModel> logger,
        IPresetCatalog catalog, IPluginHost pluginHost, OfficialPluginService official)
    {
        _alert = alert;
        _logger = logger;
        _catalog = catalog;
        _pluginHost = pluginHost;
        _official = official;

        TabFilters = new[]
        {
            "Style.Category.All", "Style.Category.Hot",
        }.Concat(StyleCategories.AllKeys).Concat(new[]
        {
            "Market.Tab.Mine",
        }).ToArray();

        _official.Changed += (_, _) => System.Windows.Application.Current?.Dispatcher.BeginInvoke(RefreshCardsAsync);
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            RebuildCatalogItems();
            foreach (var card in Cards)
                card.RefreshMeta();
        };

        RebuildCatalogItems();
        _ = RefreshCardsAsync();
        _ = _official.RefreshFromGitHubAsync(); // 网络可用时同步官方仓库最新目录
    }

    public string[] TabFilters { get; }

    /// <summary>默认落在「全部」:单一目录,官方与社区插件不再区分。</summary>
    [ObservableProperty]
    private string _tabFilter = "Style.Category.All";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private int _page = 1;

    /// <summary>每页 3 × 3。</summary>
    public const int PageSize = 9;

    public ObservableCollection<MarketCardViewModel> Cards { get; } = new();

    /// <summary>当前详情。公开 setter 拒绝 null:分页翻页时保持详情面板。</summary>
    public MarketCardViewModel? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (value is null) return;
            if (SetProperty(ref _selectedCard, value)) OnPropertyChanged(nameof(HasSelection));
        }
    }

    private MarketCardViewModel? _selectedCard;

    public bool HasSelection => SelectedCard is not null;

    public bool HasCards => Cards.Count > 0;

    /// <summary>「我的插件」空状态(Mine tab 且库为空)。</summary>
    public bool ShowMineEmpty => TabFilter == "Market.Tab.Mine" && Cards.Count == 0;

    public bool ShowGeneralEmpty => TabFilter != "Market.Tab.Mine" && Cards.Count == 0;

    public int TotalItems { get; private set; }

    public int TotalPages => Math.Max(1, (int)Math.Ceiling(TotalItems / (double)PageSize));

    public bool PagerVisible => TotalPages > 1;

    /// <summary>任一插件正在安装(全部安装按钮禁用)。</summary>
    public bool IsInstalling => _installing;

    /// <summary>右侧面板:true = 该作者的全部插件列表,false = 选中插件详情。</summary>
    [ObservableProperty]
    private bool _showAllPlugins;

    /// <summary>「作者全部插件」面板锚定的作者(打开面板时取当前详情的作者)。</summary>
    private string? _allCardsAuthor;

    public string? AllAuthorText => _allCardsAuthor;

    public ObservableCollection<MarketCardViewModel> AllCards { get; } = new();

    public int AllCount => AllCards.Count;

    public string AllHeaderText =>
        _allCardsAuthor is null
            ? string.Format(Tr.Get("Market.All.Count"), AllCards.Count)
            : $"{_allCardsAuthor} · {string.Format(Tr.Get("Market.All.Count"), AllCards.Count)}";

    partial void OnShowAllPluginsChanged(bool value)
    {
        if (value) _allCardsAuthor = SelectedCard?.Author; // 打开面板时锚定当前详情的作者
        RebuildAllCards();
    }

    /// <summary>点击列表中的条目 → 选中并回到详情。</summary>
    [RelayCommand]
    private void SelectAllCard(MarketCardViewModel card)
    {
        SelectedCard = card;
        ShowAllPlugins = false;
    }

    /// <summary>
    /// 重建「作者全部插件」列表:锚定作者 + 搜索框过滤(与搜索引擎联动)。
    /// 面板关闭时跳过,避免搜索输入时反复重建。
    /// </summary>
    private void RebuildAllCards()
    {
        if (!ShowAllPlugins) return;
        AllCards.Clear();
        var source = _catalogItems.Concat(MarketplaceCatalog.Plugins)
            .Where(p => _allCardsAuthor is null || p.Author.Equals(_allCardsAuthor, StringComparison.OrdinalIgnoreCase));
        foreach (var plugin in ApplySearch(source))
        {
            AllCards.Add(new MarketCardViewModel(this, plugin,
                plugin.PluginId is not null && _official.IsInstalled(plugin.PluginId)));
        }
        OnPropertyChanged(nameof(AllCount));
        OnPropertyChanged(nameof(AllHeaderText));
    }

    partial void OnTabFilterChanged(string value)
    {
        Page = 1;
        _ = RefreshCardsAsync();
    }

    partial void OnSearchTextChanged(string value) => _ = RefreshCardsAsync();

    partial void OnPageChanged(int value) => _ = RefreshCardsAsync();

    private List<MarketPlugin> _catalogItems = new();

    /// <summary>把目录生成为市场条目(单一目录,官方/社区不再区分;预览 = 封面图自动切分的前/后两半)。</summary>
    private void RebuildCatalogItems()
    {
        bool english = LocalizationSource.Current.Language == "en";
        _catalogItems = _official.Items.Select(item =>
        {
            uint hash = 2166136261;
            foreach (char ch in item.PluginId) { hash ^= ch; hash *= 16777619; }
            double rating = 4.2 + (hash % 8) / 10.0;
            string installs = $"{2 + hash % 8}.{hash % 10}k";
            string categoryText = Tr.Get(item.Category);
            // 作者在目录中自行配置仓库地址;官方条目缺省指向本仓库 packages 内对应的包。
            string repository = item.Repository
                ?? $"https://github.com/{OfficialPluginService.RepoOwner}/{OfficialPluginService.RepoName}" +
                   $"/tree/{OfficialPluginService.RepoBranch}/{item.PackageFile}";
            // 标签 = 分类 + 作者在目录中自配的 tags(最多展示 3 个)。
            var tags = new[] { categoryText }.Concat(item.Tags).Take(3).ToArray();

            return new MarketPlugin
            {
                Id = item.PluginId,
                PluginId = item.PluginId,
                PresetId = item.PresetId,
                PackageFile = item.PackageFile,
                Name = english ? item.NameEn : item.NameZh,
                Author = item.Author,
                IsVerified = true,
                Tags = tags,
                CategoryKey = item.Category,
                Rating = rating,
                InstallsText = installs,
                Version = item.Version,
                SizeText = Tr.Get("Market.System.BuiltIn"),
                UpdatedText = "—",
                Description = english ? item.DescEn : item.DescZh,
                PreviewImage = SampleImage,
                TileBrush = MarketPlugin.MakeBrush(item.PluginId),
                BeforeBrush = SampleBeforeBrush,
                AfterBrush = SampleAfterBrush,
                RepositoryUrl = repository,
            };
        }).ToList();
    }

    private IEnumerable<MarketPlugin> Query()
    {
        // 统一目录(官方/社区不再区分):「我的插件」= 已安装;其余 Tab = 全部目录按分类过滤。
        if (TabFilter == "Market.Tab.Mine")
            return ApplySearch(_catalogItems.Where(p => p.PluginId is not null && _official.IsInstalled(p.PluginId)));

        IEnumerable<MarketPlugin> query = _catalogItems.Concat(MarketplaceCatalog.Plugins);

        if (TabFilter == "Style.Category.Hot")
            query = query.OrderByDescending(p => ParseInstalls(p.InstallsText));
        else if (TabFilter != "Style.Category.All")
            query = query.Where(p => p.CategoryKey == TabFilter);

        return ApplySearch(query);
    }

    /// <summary>搜索覆盖 名称 / 作者 / 标签(输入作者名可找到其全部插件)。</summary>
    private IEnumerable<MarketPlugin> ApplySearch(IEnumerable<MarketPlugin> query)
    {
        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string needle = SearchText.Trim();
            query = query.Where(p =>
                p.Name.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || p.Author.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || p.Tags.Any(t => t.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }
        return query;
    }
    private async Task RefreshCardsAsync()
    {
        RebuildAllCards();
        var records = Query().ToList();
        TotalItems = records.Count;
        int page = Math.Clamp(Page, 1, TotalPages);
        var slice = records.Skip((page - 1) * PageSize).Take(PageSize);

        Cards.Clear();
        foreach (var plugin in slice)
        {
            bool installed = plugin.PluginId is not null && _official.IsInstalled(plugin.PluginId);
            Cards.Add(new MarketCardViewModel(this, plugin, installed));
        }

        // 首次进入(或当前详情为空)默认选中本 Tab 第一张。
        if (_selectedCard is null && Cards.Count > 0)
        {
            _selectedCard = Cards[0];
            OnPropertyChanged(nameof(SelectedCard));
        }

        // 详情尽量保持;当前详情不在本页时不强制切换(详情面板仍显示原选中项)。
        var next = Cards.FirstOrDefault(c => c.Model.Id == _selectedCard?.Model.Id);
        if (next is not null && !ReferenceEquals(next, _selectedCard))
        {
            _selectedCard = next;
            OnPropertyChanged(nameof(SelectedCard));
        }

        OnPropertyChanged(nameof(HasCards));
        OnPropertyChanged(nameof(ShowMineEmpty));
        OnPropertyChanged(nameof(ShowGeneralEmpty));
        OnPropertyChanged(nameof(TotalItems));
        OnPropertyChanged(nameof(TotalPages));
        OnPropertyChanged(nameof(PagerVisible));
        await Task.CompletedTask;
    }

    // ---------- 安装 / 卸载(真实宿主操作) ----------

    public async Task InstallAsync(MarketCardViewModel card)
    {
        if (card.IsInstalled || _installing) return;
        if (card.Model.PluginId is null || card.Model.PackageFile.Length == 0) return;

        // 定位插件包 → 宿主安装(内置查重)→ 发现并启用 → 预设进入风格化面板。
        _installing = true;
        card.IsInstallingThis = true;
        OnPropertyChanged(nameof(IsInstalling));
        NotifyInstallability();

        var zip = await _official.PreparePackageAsync(card.Model.PackageFile);
        if (zip is null)
        {
            _alert.Warn(Tr.Get("Market.Install"), Tr.Get("Market.Install.Offline"));
            FinishInstall(card);
            return;
        }

        var result = await _pluginHost.InstallFromPackageAsync(zip);
        if (!result.Success && result.ErrorCode != "duplicate")
        {
            _alert.Warn(Tr.Get("Market.Install"), result.Detail ?? Tr.Get("Market.Install.Failed"));
            FinishInstall(card);
            return;
        }

        await _pluginHost.DiscoverAsync();
        await _pluginHost.EnableAsync(card.Model.PluginId);
        card.IsInstalled = true;
        _logger.LogInformation("Plugin installed: {Id}", card.Model.PluginId);
        FinishInstall(card);
        await RefreshCardsAsync(); // 同步「我的插件」与右侧列表的安装标记
    }

    private void FinishInstall(MarketCardViewModel card)
    {
        card.IsInstallingThis = false;
        _installing = false;
        OnPropertyChanged(nameof(IsInstalling));
        NotifyInstallability();
    }

    public async Task UninstallAsync(MarketCardViewModel card)
    {
        if (!card.IsInstalled || _installing) return;

        // 卸载 = 从目录移除预设(释放对插件程序集的引用)+ 宿主卸载(删除插件文件夹)。
        string body = string.Format(Tr.Get("Market.Uninstall.Confirm"), card.Model.Name);
        if (!_alert.Confirm(Tr.Get("Market.Uninstall"), body)) return;

        if (card.Model.PresetId is not null)
            _catalog.RemoveById(card.Model.PresetId);
        if (card.Model.PluginId is not null)
            await _pluginHost.UninstallAsync(card.Model.PluginId);
        card.IsInstalled = false;
        _logger.LogInformation("Plugin uninstalled (deleted): {Id}", card.Model.PluginId);
        await RefreshCardsAsync(); // 同步「我的插件」与右侧列表的安装标记
    }

    public void ShowChangelog(MarketCardViewModel card)
        => _alert.Info(string.Format(Tr.Get("Market.Detail.Changelog.Todo"), card.Model.Name));

    private void NotifyInstallability()
    {
        foreach (var card in Cards)
        {
            card.InstallCommand.NotifyCanExecuteChanged();
            card.NotifyInstallStateChanged();
        }
    }

    /// <summary>安装量文案 → 数值(用于热门排序)。</summary>
    private static double ParseInstalls(string text)
    {
        var digits = new string(text.TakeWhile(ch => char.IsDigit(ch) || ch == '.').ToArray());
        if (!double.TryParse(digits, System.Globalization.CultureInfo.InvariantCulture, out var value))
            return 0;
        if (text.Contains('k') || text.Contains('K')) value *= 1000;
        if (text.Contains('m') || text.Contains('M')) value *= 1_000_000;
        return value;
    }
}
