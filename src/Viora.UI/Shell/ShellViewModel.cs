using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows.Data;
using CommunityToolkit.Mvvm.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using CommunityToolkit.Mvvm.Input;
using Viora.UI.Localization;
using Viora.UI.Theming;

namespace Viora.UI.Shell;

/// <summary>One sidebar entry. GroupKey drives the visual clustering (Create / System / Information).</summary>
public partial class NavigationItem : ObservableObject
{
    public NavigationItem(string groupKey, string titleKey, string glyph, Type pageType, int order)
    {
        GroupKey = groupKey;
        TitleKey = titleKey;
        Glyph = glyph;
        PageType = pageType;
        Order = order;
    }

    public string GroupKey { get; }

    public string TitleKey { get; }

    public string Glyph { get; }

    public Type PageType { get; }

    public int Order { get; }

    public string Title => Tr.Get(TitleKey);

    public void RefreshTitle() => OnPropertyChanged(nameof(Title));
}

/// <summary>
/// Sidebar navigation shell. Pages are resolved from DI by type and cached.
/// One grouped list (single selection — group ListBoxes would each keep their own
/// highlight and scroll position, drifting the sidebar). Responsive: LayoutState
/// drives IsSidebarCompact from the window width; the hamburger toggles SidebarOpen.
/// </summary>
public partial class ShellViewModel : ObservableObject
{
    private readonly IServiceProvider _services;

    private readonly Dictionary<Type, object> _pageCache = new();

    public ShellViewModel(IServiceProvider services)
    {
        _services = services;

        Items = new ObservableCollection<NavigationItem>
        {
            // 创作
            new(NavGroupKeys.Create, "Nav.Home", "\uE80F", typeof(Pages.Home.HomePage), 0),
            new(NavGroupKeys.Create, "Nav.Convert", "\uE8E5", typeof(Pages.Convert.ConvertPage), 1),
            // 社区(独立分组:未来承接插件分享)
            new(NavGroupKeys.Community, "Nav.Community", "\uE902", typeof(Pages.Community.CommunityPage), 2),
            // 系统
            new(NavGroupKeys.System, "Nav.Plugins", "\uE116", typeof(Pages.Plugins.PluginsPage), 3),
            new(NavGroupKeys.System, "Nav.Settings", "\uE713", typeof(Pages.Settings.SettingsPage), 4),
            // 信息(帮助与反馈内容并入关于页)
            new(NavGroupKeys.Information, "Nav.About", "\uE946", typeof(Pages.About.AboutPage), 5),
            new(NavGroupKeys.Information, "Nav.Sponsor", "\uE734", typeof(Pages.Sponsor.SponsorPage), 6),
            new(NavGroupKeys.Information, "Nav.Legal", "\uE8B7", typeof(Pages.Legal.LegalPage), 7),
        };

        var view = CollectionViewSource.GetDefaultView(Items);
        view.GroupDescriptions.Add(new PropertyGroupDescription(nameof(NavigationItem.GroupKey)));
        NavView = view;

        SelectedItem = Items[0];

        var layout = LayoutState.Current;
        layout.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(LayoutState.IsSidebarCompact) or nameof(LayoutState.SidebarOpen))
                OnPropertyChanged(nameof(IsSidebarCompact));
        };
        RecomputeCompact(layout);

        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var item in Items) item.RefreshTitle();
            OnPropertyChanged(nameof(AppTitle));
            OnPropertyChanged(nameof(AppTagline));
        };
    }

    public string AppTitle => Tr.Get("App.Name");

    public string AppTagline => Tr.Get("App.Tagline");

    public ObservableCollection<NavigationItem> Items { get; }

    /// <summary>Grouped view over Items — one ListBox, one selection, fixed layout.</summary>
    public System.ComponentModel.ICollectionView NavView { get; }

    [ObservableProperty]
    private NavigationItem? _selectedItem;

    /// <summary>True = icon rail (labels hidden). Mirror of LayoutState for binding.</summary>
    public bool IsSidebarCompact => LayoutState.Current.IsSidebarCompact;

    [RelayCommand]
    private void ToggleSidebar()
    {
        var layout = LayoutState.Current;
        // If currently auto-collapsed because the window is narrow, the toggle re-pins open;
        // otherwise it flips the pin.
        layout.SidebarOpen = layout.IsSidebarCompact;
        RecomputeCompact(layout);
    }

    private static void RecomputeCompact(LayoutState layout)
    {
        bool compact = layout.WindowWidth < LayoutState.CompactSidebarWidth || !layout.SidebarOpen;
        if (compact != layout.IsSidebarCompact) layout.IsSidebarCompact = compact;
    }

    /// <summary>Called by the shell window on SizeChanged.</summary>
    public static void UpdateLayoutSize(double width, double height)
    {
        var layout = LayoutState.Current;
        layout.UpdateSize(width, height);
        RecomputeCompact(layout);
    }

    public object? CurrentPage
    {
        get
        {
            if (SelectedItem is null) return null;
            if (!_pageCache.TryGetValue(SelectedItem.PageType, out var page))
            {
                page = _services.GetRequiredService(SelectedItem.PageType);
                _pageCache[SelectedItem.PageType] = page;
            }
            return page;
        }
    }

    partial void OnSelectedItemChanged(NavigationItem? value) => OnPropertyChanged(nameof(CurrentPage));

    [RelayCommand]
    private void Navigate(NavigationItem item) => SelectedItem = item;
}

public static class NavGroupKeys
{
    public const string Create = "Nav.Group.Create";
    public const string Community = "Nav.Group.Community";
    public const string System = "Nav.Group.System";
    public const string Information = "Nav.Group.Information";
}
