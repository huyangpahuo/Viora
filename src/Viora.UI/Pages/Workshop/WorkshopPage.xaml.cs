using System.Windows.Controls;
using System.Windows.Input;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:编辑器 Tab → 4 空格,输出框自动滚到底部。
/// </summary>
public partial class WorkshopPage : UserControl
{
    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
    }

    private void Editor_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        // Tab → 4 空格
        if (sender is not TextBox box) return;
        var change = e.Changes.FirstOrDefault(c => c.AddedLength == 1);
        if (change is null || box.SelectionStart == 0 || box.Text[box.SelectionStart - 1] != '\t') return;

        int caret = box.SelectionStart;
        box.Text = box.Text.Remove(caret - 1, 1).Insert(caret - 1, "    ");
        box.SelectionStart = caret + 3;
    }

    private void Output_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.ScrollToEnd();
    }
}
