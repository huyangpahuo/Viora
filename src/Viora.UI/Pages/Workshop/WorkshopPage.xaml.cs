using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:双层编辑器(透明输入层 + 语法高亮只读层)的滚动同步,
/// 编译状态徽章,Before/After 对比滑杆的裁剪几何。
/// </summary>
public partial class WorkshopPage : UserControl
{
    private bool _syncing;

    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        // RichTextBox.Document 非依赖属性,不能 XAML 绑定:这里手动赋值并跟随 VM 更新
        vm.HighlightDocument = CSharpHighlighter.Build(vm.SourceCode, CSharpHighlighter.TokenPalette.Instance);
        HighlightBox.Document = vm.HighlightDocument;
        vm.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(WorkshopViewModel.CompileState))
                UpdateBadge(vm.CompileState);
            else if (e.PropertyName == nameof(WorkshopViewModel.HighlightDocument) && vm.HighlightDocument is not null)
                HighlightBox.Document = vm.HighlightDocument;
        };
        UpdateBadge(vm.CompileState);
        ApplyCompareClip(vm.ComparePosition);
    }

    // ---------- 编辑层:Tab 缩进 + 高亮重建 + 滚动同步 ----------

    private void Editor_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is not TextBox box) return;

        // Tab → 4 空格
        var change = e.Changes.FirstOrDefault(c => c.AddedLength == 1);
        if (change is not null && box.SelectionStart > 0 && box.Text[box.SelectionStart - 1] == '\t')
        {
            int caret = box.SelectionStart;
            box.Text = box.Text.Remove(caret - 1, 1).Insert(caret - 1, "    ");
            box.SelectionStart = caret + 3;
        }

        if (DataContext is WorkshopViewModel vm)
        {
            _syncing = true;
            try { vm.HighlightDocument = CSharpHighlighter.Build(box.Text, CSharpHighlighter.TokenPalette.Instance); }
            finally { _syncing = false; }
        }
        Dispatcher.BeginInvoke(SyncScroll);
    }

    private void Editor_OnScroll(object sender, ScrollChangedEventArgs e)
    {
        if (_syncing) return;
        SyncScroll();
    }

    private void SyncScroll()
    {
        if (_syncing) return;
        var sv = FindScrollViewer(EditorBox);
        if (sv is null) return;
        _syncing = true;
        try
        {
            HighlightScroller.ScrollToVerticalOffset(sv.VerticalOffset);
            HighlightScroller.ScrollToHorizontalOffset(sv.HorizontalOffset);
        }
        finally { _syncing = false; }
    }

    private static System.Windows.Controls.ScrollViewer? FindScrollViewer(System.Windows.DependencyObject root)
    {
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is System.Windows.Controls.ScrollViewer sv) return sv;
            var found = FindScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    // ---------- 编译状态徽章 ----------

    private void UpdateBadge(int state)
    {
        if (state == 0)
        {
            CompileBadge.Visibility = Visibility.Collapsed;
            return;
        }
        CompileBadge.Visibility = Visibility.Visible;
        bool ok = state == 1;
        BadgeIcon.Geometry = (Geometry)FindResource(ok ? "Icon.CircleCheck" : "Icon.CircleXmark");
        BadgeIcon.Foreground = ok
            ? (Brush)FindResource("Color.Success")
            : (Brush)FindResource("Color.Danger");
        BadgeText.Text = ok ? Tr.Get("Workshop.Badge.Ok") : Tr.Get("Workshop.Badge.Failed");
    }

    // ---------- Before/After 对比 ----------

    private void CompareSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ApplyCompareClip(e.NewValue);
    }

    private void PreviewBorder_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (DataContext is WorkshopViewModel vm) ApplyCompareClip(vm.ComparePosition);
    }

    private void ApplyCompareClip(double percent)
    {
        // Image 是 Stretch=Uniform,实际显示区与容器边距未知;直接按控件宽度比例裁剪,
        // 图像两侧留白被裁掉的部分不影响观感(两侧留白对称)。
        double width = BeforeImage.ActualWidth;
        if (width <= 0) return;
        double x = width * percent / 100.0;
        BeforeImage.Clip = new RectangleGeometry(new Rect(0, 0, x, BeforeImage.ActualHeight));
        SplitLine.Height = BeforeImage.ActualHeight > 0 ? BeforeImage.ActualHeight : SplitLine.Height;
        Canvas.SetLeft(SplitLine, x);
    }

    // ---------- 输出滚底 ----------

    private void Output_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.ScrollToEnd();
    }
}
