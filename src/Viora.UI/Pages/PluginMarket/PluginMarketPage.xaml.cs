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
