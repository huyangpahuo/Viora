using System.Collections.ObjectModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viora.Core.Localization;
using Viora.Core.Settings;
using Viora.UI.Localization;

namespace Viora.UI.Shell;

/// <summary>
/// One sidebar entry. IconKey indexes the generated geometry library (Assets/Icons.xaml);
/// TitleKey/SubtitleKey are localization keys. Section drives placement: core功能在上,
/// aux(关于/帮助/反馈)固定在侧栏底部。
/// </summary>
public partial class NavigationItem : ObservableObject
{
    public NavigationItem(string section, string titleKey, string? subtitleKey, string iconKey, Type pageType, int order)
    {
        Section = section;
        TitleKey = titleKey;
        SubtitleKey = subtitleKey;
        IconKey = iconKey;
        PageType = pageType;
        Order = order;
    }

    public string Section { get; }

    public string TitleKey { get; }

    public string? SubtitleKey { get; }

    public string IconKey { get; }

    public Type PageType { get; }

    public int Order { get; }

    public string Title => Tr.Get(TitleKey);

    public string? Subtitle => SubtitleKey is null ? null : Tr.Get(SubtitleKey);

    public bool HasSubtitle => SubtitleKey is not null;

    public void Refresh() { OnPropertyChanged(nameof(Title)); OnPropertyChanged(nameof(Subtitle)); }
}

/// <summary>
/// Sidebar navigation + integrated top bar (brand / language / window controls).
/// Pages resolve from DI by type and cache; placeholder pages inherit the nav title.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;
    private readonly ILocalizationService _localization;
    private readonly ISettingsService _settings;
    private readonly ILogger<ShellViewModel> _logger;

    private readonly Dictionary<Type, object> _pageCache = new();

    public ShellViewModel(IServiceProvider services, ILocalizationService localization, ISettingsService settings,
        ILogger<ShellViewModel> logger)
    {
        _services = services;
        _localization = localization;
        _settings = settings;
        _logger = logger;

        Items = new ObservableCollection<NavigationItem>
        {
            new(Sections.Core, "Nav.Stylize", "Nav.Stylize.Sub", "WandMagicSparkles", typeof(Pages.Stylize.StylizePage), 0),
            new(Sections.Core, "Nav.MyWorks", "Nav.MyWorks.Sub", "Images", typeof(Pages.MyWorks.MyWorksPage), 1),
            new(Sections.Core, "Nav.PluginMarket", "Nav.PluginMarket.Sub", "PuzzlePiece", typeof(Pages.PluginMarket.PluginMarketPage), 2),
            new(Sections.Core, "Nav.Settings", "Nav.Settings.Sub", "Gear", typeof(Pages.Settings.SettingsPage), 3),
            new(Sections.Aux, "Nav.About", null, "CircleInfo", typeof(Pages.Placeholder.PlaceholderPage), 4),
            new(Sections.Aux, "Nav.Help", null, "CircleQuestion", typeof(Pages.Placeholder.PlaceholderPage), 5),
            new(Sections.Aux, "Nav.Feedback", null, "Envelope", typeof(Pages.Placeholder.PlaceholderPage), 6),
        };

        SelectedItem = Items[0];

        Languages = localization.AvailableLanguages.ToList();
        RefreshLanguageState();

        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var item in Items) item.Refresh();
            OnPropertyChanged(nameof(AppTitle));
            OnPropertyChanged(nameof(AppTagline));
            RefreshLanguageState();
        };
    }

    public static class Sections
    {
        public const string Core = "Core";
        public const string Aux = "Aux";
    }

    public string AppTitle => Tr.Get("App.Name");

    public string AppTagline => Tr.Get("App.Tagline");

    public ObservableCollection<NavigationItem> Items { get; }

    /// <summary>Core功能(风格化/我的作品/插件市场/设置)。</summary>
    public IEnumerable<NavigationItem> CoreItems => Items.Where(i => i.Section == Sections.Core);

    /// <summary>底部辅助导航(关于/帮助/反馈)。</summary>
    public IEnumerable<NavigationItem> AuxItems => Items.Where(i => i.Section == Sections.Aux);

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    // ---------- 顶栏:语言 ----------

    public IReadOnlyList<string> Languages { get; }

    [ObservableProperty]
    private string _currentLanguageName = string.Empty;

    private void RefreshLanguageState() => CurrentLanguageName = LanguageDisplayName(_localization.CurrentLanguage);

    public static string LanguageDisplayName(string code) => code switch
    {
        "zh-Hans" or "zh" => "中文",
        "en" => "English",
        _ => code,
    };

    [RelayCommand]
    private void SetLanguage(string code)
    {
        if (code == _localization.CurrentLanguage) return;
        _settings.Current.Language.Language = code;
        _ = _settings.SaveAsync();
        _localization.SetLanguage(code);
        RefreshLanguageState();
    }

    // ---------- 顶栏:窗口控制 ----------

    [RelayCommand]
    private void Minimize()
    {
        var win = Application.Current.MainWindow;
        if (win is not null) win.WindowState = WindowState.Minimized;
    }

    [RelayCommand]
    private void MaximizeOrRestore()
    {
        var win = Application.Current.MainWindow;
        if (win is null) return;
        win.WindowState = win.WindowState == WindowState.Maximized ? WindowState.Normal : WindowState.Maximized;
    }

    [RelayCommand]
    private void CloseApp() => Application.Current.MainWindow?.Close();

    public bool IsMaximized => Application.Current.MainWindow?.WindowState == WindowState.Maximized;

    /// <summary>ShellView 监听到窗口 StateChanged 时调用,刷新最大化/还原图标。</summary>
    public void RefreshWindowState() => OnPropertyChanged(nameof(IsMaximized));

    // ---------- 页面承载 ----------

    public object? CurrentPage
    {
        get
        {
            if (SelectedItem is null) return null;
            if (!_pageCache.TryGetValue(SelectedItem.PageType, out var page))
            {
                try
                {
                    page = _services.GetRequiredService(SelectedItem.PageType);
                }
                catch (Exception ex)
                {
                    // 绑定引擎会吞掉属性 getter 的异常(页面静默空白),这里必须显式记录。
                    _logger.LogError(ex, "Failed to create page {PageType}", SelectedItem.PageType.Name);
                    return null;
                }
                _pageCache[SelectedItem.PageType] = page;
            }
            // 占位页共享一个类,标题/副标题每次切换都要跟随当前导航项。
            if (page is Pages.Placeholder.PlaceholderPage placeholder)
            {
                placeholder.TitleKey = SelectedItem.TitleKey;
                placeholder.SubtitleKey = SelectedItem.SubtitleKey;
            }
            return page;
        }
    }

    partial void OnSelectedItemChanged(NavigationItem? value) => OnPropertyChanged(nameof(CurrentPage));

    /// <summary>跨页跳转(如“我的作品 → 重新生成”回到风格化页)。</summary>
    public void NavigateTo(string titleKey)
    {
        var item = Items.FirstOrDefault(i => i.TitleKey == titleKey);
        if (item is not null) SelectedItem = item;
    }
}
