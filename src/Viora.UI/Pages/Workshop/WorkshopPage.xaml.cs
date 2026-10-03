using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:
/// - 双层编辑器(透明输入层 + 语法高亮只读层),编辑层内 ScrollViewer 滚动镜像到高亮层;
/// - 编译状态徽章;
/// - Before/After 对比:预览框圆角裁剪 + 拖动分割线裁剪左半原图;
/// - 输出面板 编译/运行 Tab 切换。
/// </summary>
public partial class WorkshopPage : UserControl
{
    private bool _syncing;

    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;

        // RichTextBox.Document 非依赖属性,不能 XAML 绑定:手动赋值并跟随 VM 更新
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
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 编辑层的内部 ScrollViewer:滚轮/拖动滚动时把偏移镜像到高亮层
        var editorScroll = FindScrollViewer(EditorBox);
        if (editorScroll is not null)
            editorScroll.ScrollChanged += (_, args) =>
            {
                if (_syncing) return;
                _syncing = true;
                try
                {
                    HighlightScroller.ScrollToVerticalOffset(args.VerticalOffset);
                    HighlightScroller.ScrollToHorizontalOffset(args.HorizontalOffset);
                }
                finally { _syncing = false; }
            };
        ApplyRoundedClips();
        ApplyCompareClip(DataContext is WorkshopViewModel vm ? vm.ComparePosition : 50);
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

    // ---------- 编辑层:Tab 缩进 + 高亮重建 ----------

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

    // ---------- 输出 Tab ----------

    private void OutTab_OnChecked(object sender, RoutedEventArgs e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        vm.SetOutputTab(TabBuild.IsChecked == true ? "build" : "run");
    }

    // ---------- Before/After 对比 + 圆角裁剪 ----------

    private void PreviewFrame_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyRoundedClips();
        if (DataContext is WorkshopViewModel vm) ApplyCompareClip(vm.ComparePosition);
    }

    private void CompareSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ApplyCompareClip(e.NewValue);
    }

    /// <summary>预览框按实际尺寸设置圆角裁剪(Border.CornerRadius 不裁剪子元素,必须用 Clip)。</summary>
    private void ApplyRoundedClips()
    {
        double w = PreviewFrame.ActualWidth, h = PreviewFrame.ActualHeight;
        if (w <= 0 || h <= 0) return;
        PreviewFrame.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 10, 10);
    }

    private void ApplyCompareClip(double percent)
    {
        double w = BeforeImage.ActualWidth, h = BeforeImage.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double x = w * percent / 100.0;
        BeforeImage.Clip = new RectangleGeometry(new Rect(0, 0, x, h));
        SplitLine.Height = h;
        Canvas.SetLeft(SplitLine, Math.Clamp(x - 1, 0, Math.Max(0, w - 2)));
    }

    // ---------- 输出滚底 ----------

    private void Output_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.ScrollToEnd();
    }
}
