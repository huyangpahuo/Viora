using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace Viora.UI.Behaviors;

/// <summary>
/// Keeps the mouse wheel reserved for page scrolling: when the wheel target is a
/// Slider (which would otherwise eat the wheel while focused) or a ListBox that
/// cannot scroll on its own, the event is forwarded to the hosting ScrollViewer.
/// Attach to the page-level ScrollViewer (PageChrome template does this).
/// </summary>
public static class WheelBehavior
{
    public static readonly DependencyProperty EnabledProperty = DependencyProperty.RegisterAttached(
        "Enabled", typeof(bool), typeof(WheelBehavior), new PropertyMetadata(false, OnEnabledChanged));

    public static bool GetEnabled(DependencyObject obj) => (bool)obj.GetValue(EnabledProperty);

    public static void SetEnabled(DependencyObject obj, bool value) => obj.SetValue(EnabledProperty, value);

    private static void OnEnabledChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
    {
        if (d is not ScrollViewer viewer) return;
        if ((bool)e.NewValue) viewer.PreviewMouseWheel += OnPreviewWheel;
        else viewer.PreviewMouseWheel -= OnPreviewWheel;
    }

    private static void OnPreviewWheel(object sender, MouseWheelEventArgs e)
    {
        if (e.Handled || sender is not ScrollViewer page) return;

        // Sliders must never capture the wheel — page scrolling wins.
        if (FindAncestor<Slider>(e.OriginalSource as DependencyObject) is not null)
        {
            e.Handled = true;
            ScrollBy(page, e.Delta);
            return;
        }

        // ListBoxes forward to the page only when they cannot scroll themselves
        // (e.g. settings category list, preset list).
        if (FindAncestor<ListBox>(e.OriginalSource as DependencyObject) is { } listBox)
        {
            var host = FindDescendantScrollViewer(listBox);
            if (host is not null && host.ScrollableHeight > 1) return; // let it scroll itself
            e.Handled = true;
            ScrollBy(page, e.Delta);
        }
    }

    private static void ScrollBy(ScrollViewer page, int delta)
    {
        double pixels = -delta / 120.0 * 96; // ~96px per notch
        page.ScrollToVerticalOffset(page.VerticalOffset + pixels);
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject start)
    {
        int count = VisualTreeHelper.GetChildrenCount(start);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(start, i);
            if (child is ScrollViewer sv) return sv;
            var nested = FindDescendantScrollViewer(child);
            if (nested is not null) return nested;
        }
        return null;
    }

    private static T? FindAncestor<T>(DependencyObject? start) where T : DependencyObject
    {
        var current = start;
        while (current is not null)
        {
            if (current is T match) return match;
            current = VisualTreeHelper.GetParent(current);
        }
        return null;
    }
}
