using CommunityToolkit.Mvvm.ComponentModel;

namespace Viora.UI.Theming;

/// <summary>
/// Shared responsive-layout state, updated by the shell on window resize and consumed
/// by pages/VMs so every view adapts (narrow ⇄ wide, compact sidebar) from one source.
/// </summary>
public partial class LayoutState : ObservableObject
{
    private static LayoutState? _current;
    public static LayoutState Current => _current ??= new LayoutState();

    public const double NarrowWindowWidth = 960;   // below: single-column layouts
    public const double CompactSidebarWidth = 900; // below: icons-only sidebar

    [ObservableProperty]
    private double _windowWidth = 1280;

    [ObservableProperty]
    private double _windowHeight = 800;

    /// <summary>User hamburger preference (pinned open); auto-overridden when narrow.</summary>
    [ObservableProperty]
    private bool _sidebarOpen = true;

    [ObservableProperty]
    private bool _isNarrow;

    /// <summary>Icons-only sidebar (window narrow or user collapsed).</summary>
    [ObservableProperty]
    private bool _isSidebarCompact;

    partial void OnWindowWidthChanged(double value)
    {
        bool narrow = value < NarrowWindowWidth;
        if (narrow != IsNarrow) IsNarrow = narrow;

        bool compact = value < CompactSidebarWidth || !SidebarOpen;
        if (compact != IsSidebarCompact) IsSidebarCompact = compact;
    }

    partial void OnSidebarOpenChanged(bool value)
    {
        bool compact = WindowWidth < CompactSidebarWidth || !value;
        if (compact != IsSidebarCompact) IsSidebarCompact = compact;
    }

    public void UpdateSize(double width, double height)
    {
        WindowWidth = width;
        WindowHeight = height;
    }
}
