using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using Viora.UI.Localization;
using Viora.UI.Pages.MyWorks;

namespace Viora.UI.Pages.MyWorks;

/// <summary>
/// 我的作品 code-behind:卡片“更多”菜单(低频操作收敛进菜单)与详情标题编辑提交。
/// </summary>
public partial class MyWorksPage : UserControl
{
    public MyWorksPage(MyWorksViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    private MyWorksViewModel ViewModel => (MyWorksViewModel)DataContext;

    /// <summary>卡片/详情的“更多”:弹出深色菜单(重新生成 / 副本 / 打开文件夹 / 收藏 / 删除)。</summary>
    private void OnMoreClick(object sender, RoutedEventArgs e)
    {
        // 列表卡片 DataContext = WorkCardViewModel;详情面板 DataContext = 页面 VM,取 SelectedCard。
        var card = (sender as FrameworkElement)?.DataContext switch
        {
            WorkCardViewModel c => c,
            MyWorksViewModel vm => vm.SelectedCard,
            _ => null,
        };
        if (card is null) return;

        var menu = new ContextMenu { Style = (Style)FindResource("DarkContextMenu") };

        menu.Items.Add(MakeItem(card.RegenerateCommand, "Works.Menu.Regenerate", card));
        menu.Items.Add(MakeItem(card.CopyCommand, "Works.Menu.Copy", card));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem(card.OpenFolderCommand, "Works.Menu.OpenFolder", card));
        menu.Items.Add(MakeItem(card.ToggleFavoriteCommand, card.Record.IsFavorite ? "Works.Card.Unfavorite" : "Works.Card.Favorite", card));
        menu.Items.Add(new Separator());
        menu.Items.Add(MakeItem(card.DeleteCommand, "Works.Menu.Delete", card));

        menu.PlacementTarget = (UIElement)sender;
        menu.Placement = PlacementMode.Bottom;
        menu.IsOpen = true;
    }

    private static MenuItem MakeItem(ICommand command, string headerKey, WorkCardViewModel card)
    {
        var item = new MenuItem
        {
            Header = Tr.Get(headerKey),
            Command = command,
            CommandParameter = card,
        };
        item.SetResourceReference(Control.StyleProperty, "DarkMenuItem");
        return item;
    }

    /// <summary>作品卡片列表滚轮:外层 ScrollViewer 统一滚动。</summary>
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

    private void OnTitleCommit(object sender, RoutedEventArgs e) => ViewModel.CommitTitleEdit();

    private void OnTitleEnter(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            Keyboard.ClearFocus();
            ViewModel.CommitTitleEdit();
            e.Handled = true;
        }
    }
}
