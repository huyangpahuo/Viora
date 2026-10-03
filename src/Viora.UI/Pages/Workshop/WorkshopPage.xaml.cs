using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:
/// - 编辑器 = 高亮只读层(RichTextBox,内滚)+ 输入层(TextBox,NoWrap);两层行高/列宽严格一致,
///   输入层内部 ScrollViewer 的偏移实时镜像到高亮层内部 ScrollViewer,横纵皆可滚动;
/// - 高亮/minimap/registry 三份流文档均由 VM 生成,这里赋给对应 RichTextBox(Document 非依赖属性);
/// - 文件列表 / minimap 折叠切换;编译状态徽章;Before/After 圆角与分割裁剪;输出 Tab。
/// </summary>
public partial class WorkshopPage : UserControl
{
    private const double CodeLineHeight = 17;
    private bool _syncing;

    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += OnLoaded;

        // 输入层行高与高亮层段落行高强制一致(像素级对齐的前提)
        System.Windows.Controls.TextBlock.SetLineHeight(EditorBox, CodeLineHeight);
        System.Windows.Controls.TextBlock.SetLineStackingStrategy(EditorBox, System.Windows.LineStackingStrategy.BlockLineHeight);

        // RichTextBox.Document 非依赖属性:手动赋值并跟随 VM 更新
        vm.HighlightDocument = CSharpHighlighter.Build(vm.SourceCode, CSharpHighlighter.TokenPalette.Instance);
        vm.MinimapDocument = CSharpHighlighter.Build(vm.SourceCode, CSharpHighlighter.TokenPalette.Instance, minimap: true);
        HighlightBox.Document = vm.HighlightDocument;
        MinimapBox.Document = vm.MinimapDocument;
        if (vm.RegistryDocument is not null) RegistryBox.Document = vm.RegistryDocument;
        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WorkshopViewModel.CompileState):
                    UpdateBadge(vm.CompileState);
                    break;
                case nameof(WorkshopViewModel.HighlightDocument) when vm.HighlightDocument is not null:
                    HighlightBox.Document = vm.HighlightDocument;
                    break;
                case nameof(WorkshopViewModel.MinimapDocument) when vm.MinimapDocument is not null:
                    MinimapBox.Document = vm.MinimapDocument;
                    break;
                case nameof(WorkshopViewModel.RegistryDocument):
                    RegistryBox.Document = vm.RegistryDocument
                        ?? CSharpHighlighter.Build("", CSharpHighlighter.TokenPalette.Instance);
                    break;
                case nameof(WorkshopViewModel.ActiveTab):
                    bool preview = vm.ActiveTab == "preview";
                    SourceHost.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;
                    PreviewHost.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
                    if (!preview) Dispatcher.BeginInvoke(ApplyRoundedClips);
                    break;
                case nameof(WorkshopViewModel.ShowMinimap):
                    ApplyCollapseState(vm);
                    break;
                case nameof(WorkshopViewModel.ShowFileList):
                    ApplyCollapseState(vm);
                    break;
            }
        };
        UpdateBadge(vm.CompileState);
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        // 输入层内部 ScrollViewer 滚动 → 镜像到高亮层内部 ScrollViewer(横纵)
        var editorScroll = FindScrollViewer(EditorBox);
        var highlightScroll = FindScrollViewer(HighlightBox);
        if (editorScroll is not null && highlightScroll is not null)
            editorScroll.ScrollChanged += (_, args) =>
            {
                if (_syncing) return;
                _syncing = true;
                try
                {
                    highlightScroll.ScrollToVerticalOffset(args.VerticalOffset);
                    highlightScroll.ScrollToHorizontalOffset(args.HorizontalOffset);
                }
                finally { _syncing = false; }
            };

        // 高亮文档不换行:页宽放大(与输入层 NoWrap 对齐)
        if (HighlightBox.Document is { } doc) doc.PageWidth = 5000;

        ApplyRoundedClips();
        if (DataContext is WorkshopViewModel vm)
        {
            ApplyCollapseState(vm);
            ApplyCompareClip(vm.ComparePosition);
        }
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

    // ---------- 折叠状态:文件列表 / minimap ----------

    private void ApplyCollapseState(WorkshopViewModel vm)
    {
        FileListColumn.Width = vm.ShowFileList ? new GridLength(170) : new GridLength(34);
        FileListExpand.Visibility = vm.ShowFileList ? Visibility.Collapsed : Visibility.Visible;

        MinimapColumn.Width = vm.ShowMinimap ? new GridLength(72) : new GridLength(34);
        MinimapHost.Visibility = vm.ShowMinimap ? Visibility.Visible : Visibility.Collapsed;
        MinimapExpand.Visibility = vm.ShowMinimap ? Visibility.Collapsed : Visibility.Visible;
    }

    // ---------- 编辑层:Tab 缩进 + 防抖高亮由 VM 驱动 ----------

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
    }

    // ---------- 文件名:Enter 提交 ----------

    private void FileName_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key is Key.Enter or Key.Return && sender is TextBox box)
        {
            box.MoveFocus(new TraversalRequest(FocusNavigationDirection.Next));
            e.Handled = true;
        }
    }

    private void FileName_OnLostFocus(object sender, RoutedEventArgs e) { /* 绑定即提交 */ }

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

    // ---------- 视图切换 ----------

    private void ViewTab_OnChanged(object sender, RoutedEventArgs e)
    {
        Dispatcher.BeginInvoke(() =>
        {
            ApplyRoundedClips();
            if (DataContext is WorkshopViewModel vm) ApplyCompareClip(vm.ComparePosition);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
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
