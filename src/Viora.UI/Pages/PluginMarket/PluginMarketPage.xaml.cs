using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Viora.UI.Pages.PluginMarket;

/// <summary>
/// 插件市场 code-behind:卡片网格滚轮滚动。
/// 注意:所有命令都挂在卡片 VM 上(无 XAML CommandParameter 类型错配风险)。
/// </summary>
public partial class PluginMarketPage : UserControl
{
    public PluginMarketPage(PluginMarketViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    private int _dragDepth;

    private void ShowDropOverlay(bool show)
    {
        DropOverlay.Visibility = show ? Visibility.Visible : Visibility.Collapsed;
        // 对蒙层以外的根子元素加模糊(蒙层自身不能被糊掉)
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(PageRoot);
        for (int i = 0; i < n; i++)
        {
            if (System.Windows.Media.VisualTreeHelper.GetChild(PageRoot, i) is UIElement child && child != DropOverlay)
                child.Effect = show ? new System.Windows.Media.Effects.BlurEffect { Radius = 10 } : null;
        }
        if (!show) _dragDepth = 0;
    }

    private void Page_OnDragEnter(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop)) return;
        _dragDepth++;
        ShowDropOverlay(true);
    }

    private void Page_OnDragLeave(object sender, DragEventArgs e)
    {
        _dragDepth = Math.Max(0, _dragDepth - 1);
        if (_dragDepth == 0) ShowDropOverlay(false);
    }

    private void Page_OnDragOver(object sender, DragEventArgs e)
    {
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private async void Page_OnDrop(object sender, DragEventArgs e)
    {
        ShowDropOverlay(false);
        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0) return;
        e.Handled = true;
        if (DataContext is PluginMarketViewModel vm)
            await vm.InstallDroppedAsync(files);
    }

    /// <summary>插件卡片列表滚轮:外层 ScrollViewer 统一滚动。</summary>
    private void OnChipsWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ListBox lb)
        {
            var sv = FindDescendantScrollViewer(lb);
            if (sv is not null)
            {
                sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta * 0.3);
                e.Handled = true;
            }
        }
    }

    private static System.Windows.Controls.ScrollViewer? FindDescendantScrollViewer(System.Windows.DependencyObject root)
    {
        int count = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ScrollViewer sv) return sv;
            var found = FindDescendantScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    private void OnListWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.6);
            e.Handled = true;
        }
    }
}
