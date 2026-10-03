using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:AvalonEdit 编辑器(C# 高亮 + 行号 + Ctrl 滚轮字号 + JetBrains/Viora 配色
/// + 实时纠错波浪线),覆盖式文件列表/minimap(浮层,标题行开关),风格化同款对比查看器
/// (拖动分割把手 + 缩放),registry 高亮预览。
/// </summary>
public partial class WorkshopPage : UserControl
{
    private System.Windows.Controls.ScrollViewer? _editorScroll;
    private bool _splitDragging;
    private double _zoom = 1.0;
    private readonly ErrorBackgroundRenderer ErrorRenderer = new();

    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        // AvalonEdit:C# 高亮 + 制表符转空格 + 初始字号
        Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".cs");
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
        Editor.FontSize = vm.EditorFontSize;
        ApplyHighlightPalette(vm.JetBrainsHighlight);
        Editor.Text = vm.SourceCode;

        // 实时纠错标记
        Editor.TextArea.TextView.BackgroundRenderers.Add(ErrorRenderer);
        vm.AnalysisCompleted += (_, errors) => Dispatcher.BeginInvoke(() =>
        {
            ErrorRenderer.SetSegments(errors);
            Editor.TextArea.TextView.InvalidateLayer(KnownLayer.Background);
        });

        if (vm.MinimapLines.Count > 0) MinimapItemsHost.ItemsSource = vm.MinimapLines;

        vm.PropertyChanged += (_, e) =>
        {
            switch (e.PropertyName)
            {
                case nameof(WorkshopViewModel.CompileState):
                    UpdateBadge(vm.CompileState);
                    break;
                case nameof(WorkshopViewModel.SourceCode):
                    if (Editor.Text != vm.SourceCode) Editor.Text = vm.SourceCode;
                    break;
                case nameof(WorkshopViewModel.EditorFontSize):
                    Editor.FontSize = vm.EditorFontSize;
                    break;
                case nameof(WorkshopViewModel.MinimapLines):
                    MinimapItemsHost.ItemsSource = vm.MinimapLines;
                    break;
                case nameof(WorkshopViewModel.RegistryText):
                    RegistryBox.Document = string.IsNullOrEmpty(vm.RegistryText)
                        ? new System.Windows.Documents.FlowDocument(
                            new System.Windows.Documents.Paragraph(
                                new System.Windows.Documents.Run("")))
                        : JsonHighlighter.Build(vm.RegistryText);
                    break;
                case nameof(WorkshopViewModel.ActiveTab):
                    bool preview = vm.ActiveTab == "preview";
                    SourceHost.Visibility = preview ? Visibility.Collapsed : Visibility.Visible;
                    PreviewHost.Visibility = preview ? Visibility.Visible : Visibility.Collapsed;
                    if (!preview) Dispatcher.BeginInvoke(ApplyRoundedClips);
                    break;
                case nameof(WorkshopViewModel.ShowMinimap):
                    MinimapHost.Visibility = vm.ShowMinimap ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(WorkshopViewModel.ShowFileList):
                    FileListHost.Visibility = vm.ShowFileList ? Visibility.Visible : Visibility.Collapsed;
                    break;
                case nameof(WorkshopViewModel.ComparePosition):
                    Dispatcher.BeginInvoke(() => ApplyCompareClip(vm.ComparePosition));
                    break;
            }
        };
        UpdateBadge(vm.CompileState);
        Loaded += (_, _) =>
        {
            ApplyHighlightPalette(vm.JetBrainsHighlight);
            _editorScroll = FindScrollViewer(Editor);
            if (_editorScroll is not null)
                _editorScroll.ScrollChanged += (_, _) => UpdateMinimapViewport();
            UpdateMinimapViewport();
        };
    }

    // ---------- JetBrains / Viora 高亮配色 ----------

    /// <summary>对 AvalonEdit 内置 C# 定义按命名色重上色(JetBrains Dark 或 Viora 调色板)。</summary>
    public void ApplyHighlightPalette(bool jetbrains)
    {
        if (Editor.SyntaxHighlighting is null) return;
        foreach (var c in Editor.SyntaxHighlighting.NamedHighlightingColors)
        {
            var n = (c.Name ?? string.Empty).ToLowerInvariant();
            Color? color = null;
            if (n.Contains("comment")) color = FromHex(jetbrains ? "#808080" : "#8A9179");
            else if (n.Contains("string")) color = FromHex(jetbrains ? "#6A8759" : "#E8A2A2");
            else if (n.Contains("digit")) color = FromHex(jetbrains ? "#6897BB" : "#D8B27C");
            else if (n.Contains("keyword") || n.Contains("visibility") || n.Contains("preprocessor"))
                color = FromHex(jetbrains ? "#CC7832" : "#C58FFF");
            if (color is not null)
                c.Foreground = new SimpleHighlightingBrush(color.Value);
        }
    }

    private static Color FromHex(string hex) =>
        (Color)(ColorConverter.ConvertFromString(hex) ?? Colors.Gray);

    // ---------- Ctrl+滚轮字号 ----------

    private void Editor_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if (!Keyboard.Modifiers.HasFlag(ModifierKeys.Control)) return;
        vm.EditorFontSize = Math.Clamp(vm.EditorFontSize + (e.Delta > 0 ? 1 : -1), 9, 24);
        e.Handled = true;
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

    // ---------- Minimap:点击跳行 / 滚轮滚动 / 视口指示 ----------

    private void Minimap_MouseDown(object sender, MouseButtonEventArgs e)
    {
        JumpEditorToMinimap(e.GetPosition(MinimapScroll).Y);
        e.Handled = true;
    }

    private void Minimap_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        MinimapScroll.ScrollToVerticalOffset(MinimapScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void JumpEditorToMinimap(double yInMinimap)
    {
        var miniScroll = MinimapScroll;
        double contentY = yInMinimap + miniScroll.VerticalOffset;
        int line = (int)(contentY / 3.0);
        Editor.ScrollTo(Math.Clamp(line + 1, 1, Editor.Document.LineCount), 0);
        e_FocusEditor();
    }

    private void e_FocusEditor() { /* 保持输入焦点在编辑器(可选) */ }

    /// <summary>minimap 视口指示:半透明矩形标出编辑器可见行范围(按行数,行高 3px)。</summary>
    private void UpdateMinimapViewport()
    {
        if (_editorScroll is null) return;
        var lines = Editor.TextArea.TextView.VisualLines;
        if (lines.Count == 0) return;
        int firstLine = (int)lines[0].FirstDocumentLine.LineNumber - 1;
        int lastLine = (int)lines[^1].LastDocumentLine.LineNumber;
        int visible = Math.Max(1, lastLine - firstLine);

        double top = firstLine * 3.0;
        MinimapViewport.Width = MinimapScroll.ActualWidth;
        MinimapViewport.Height = Math.Max(6, visible * 3.0);
        System.Windows.Controls.Canvas.SetLeft(MinimapViewport, 0);
        System.Windows.Controls.Canvas.SetTop(MinimapViewport, top);

        // 指示条滚出视图时让 minimap 跟随
        double viewTop = MinimapScroll.VerticalOffset, viewBottom = viewTop + MinimapScroll.ViewportHeight;
        if (top < viewTop) MinimapScroll.ScrollToVerticalOffset(top);
        else if (top + MinimapViewport.Height > viewBottom)
            MinimapScroll.ScrollToVerticalOffset(top + MinimapViewport.Height - MinimapScroll.ViewportHeight);
    }

    // ---------- 编辑器:文本回写 VM ----------

    private void Editor_OnTextChanged(object sender, EventArgs e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if (string.Equals(Editor.Text, vm.SourceCode, StringComparison.Ordinal)) return;
        vm.SourceCode = Editor.Text;
    }

    // ---------- 文件重命名:双击进入编辑,Enter 提交 / Esc 取消 / 失焦提交 ----------

    private void FileName_OnDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WorkshopViewModel.WorkshopFile file)
        {
            file.BeginRename();
            Dispatcher.BeginInvoke(() =>
            {
                var container = FileList.ItemContainerGenerator.ContainerFromItem(file) as ContentPresenter;
                FindDescendant<TextBox>(container)?.Focus();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
    }

    private void RenameBox_OnKeyDown(object sender, KeyEventArgs e)
    {
        if (sender is not TextBox box) return;
        if (box.DataContext is not WorkshopViewModel.WorkshopFile file) return;
        if (e.Key is Key.Enter or Key.Return)
        {
            file.CommitRename();
            e.Handled = true;
        }
        else if (e.Key == Key.Escape)
        {
            file.CancelRename();
            e.Handled = true;
        }
    }

    private void RenameBox_OnLostFocus(object sender, RoutedEventArgs e)
    {
        if (sender is TextBox box && box.DataContext is WorkshopViewModel.WorkshopFile file)
            file.CommitRename();
    }

    private static T? FindDescendant<T>(System.Windows.DependencyObject? root) where T : class
    {
        if (root is null) return null;
        int n = System.Windows.Media.VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < n; i++)
        {
            var child = System.Windows.Media.VisualTreeHelper.GetChild(root, i);
            if (child is T hit) return hit;
            var found = FindDescendant<T>(child);
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

    // ---------- 视图切换 ----------

    private void ViewTab_OnChanged(object sender, RoutedEventArgs e)
    {
        var tab = (sender as FrameworkElement)?.Tag?.ToString() ?? "code";
        if (DataContext is WorkshopViewModel vm)
            vm.SwitchTabCommand.Execute(tab);
        Dispatcher.BeginInvoke(() =>
        {
            ApplyRoundedClips();
            if (DataContext is WorkshopViewModel vm) ApplyCompareClip(vm.ComparePosition);
        }, System.Windows.Threading.DispatcherPriority.Loaded);
    }

    // ---------- 对比查看器:分割拖动 + 缩放 ----------


    private void PreviewFrame_MouseDown(object sender, MouseButtonEventArgs e)
    {
        _splitDragging = true;
        ((UIElement)sender).CaptureMouse();
        UpdateSplitFromX(e.GetPosition(PreviewFrame).X);
    }

    private void PreviewFrame_MouseMove(object sender, MouseEventArgs e)
    {
        if (!_splitDragging) return;
        UpdateSplitFromX(e.GetPosition(PreviewFrame).X);
    }

    private void PreviewFrame_MouseUp(object sender, MouseButtonEventArgs e)
    {
        _splitDragging = false;
        ((UIElement)sender).ReleaseMouseCapture();
    }

    private void UpdateSplitFromX(double x)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        double w = PreviewFrame.ActualWidth;
        if (w <= 0) return;
        vm.ComparePosition = Math.Clamp(x / w * 100.0, 0, 100);
    }

    private void ZoomIn_OnClick(object sender, RoutedEventArgs e) => SetZoom(_zoom + 0.25);
    private void ZoomOut_OnClick(object sender, RoutedEventArgs e) => SetZoom(_zoom - 0.25);
    private void ZoomFit_OnClick(object sender, RoutedEventArgs e) => SetZoom(1.0);

    private void SetZoom(double v)
    {
        _zoom = Math.Clamp(v, 1.0, 4.0);
        PreviewScale.ScaleX = _zoom;
        PreviewScale.ScaleY = _zoom;
    }

    private void CompareSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        ApplyCompareClip(e.NewValue);
    }

    private void PreviewFrame_OnSizeChanged(object sender, SizeChangedEventArgs e)
    {
        ApplyRoundedClips();
        if (DataContext is WorkshopViewModel vm) ApplyCompareClip(vm.ComparePosition);
    }

    // ---------- 预览参数调节 ----------

    private void ParamSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if ((sender as FrameworkElement)?.DataContext is WorkshopViewModel.PreviewParam p)
            vm.SetPreviewParam(p.Key, e.NewValue);
    }

    /// <summary>预览框按实际尺寸设置圆角裁剪(Border.CornerRadius 不裁剪子元素,必须用 Clip)。</summary>
    private void ApplyRoundedClips()
    {
        double w = PreviewFrame.ActualWidth, h = PreviewFrame.ActualHeight;
        if (w <= 0 || h <= 0) return;
        PreviewFrame.Clip = new RectangleGeometry(new Rect(0, 0, w, h), 10, 10);
    }

    /// <summary>按 ComparePosition 裁剪左半原图,并定位分割线与把手。</summary>
    private void ApplyCompareClip(double percent)
    {
        double w = PreviewFrame.ActualWidth, h = PreviewFrame.ActualHeight;
        if (w <= 0 || h <= 0) return;
        double x = w * percent / 100.0;
        BeforeImage.Clip = new RectangleGeometry(new Rect(0, 0, x, h));
        SplitLine.Height = h;
        Canvas.SetLeft(SplitLine, Math.Clamp(x - 1, 0, Math.Max(0, w - 2)));
        Canvas.SetLeft(SplitHandle, Math.Clamp(x - 11, 0, Math.Max(0, w - 22)));
        Canvas.SetTop(SplitHandle, h / 2 - 11);
        Canvas.SetLeft(SplitHandleIcon, Math.Clamp(x - 6, 0, Math.Max(0, w - 12)));
        Canvas.SetTop(SplitHandleIcon, h / 2 - 8);
    }

    // ---------- 输出滚底 ----------

    private void Output_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (sender is TextBox box) box.ScrollToEnd();
    }
}
