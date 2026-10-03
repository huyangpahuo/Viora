using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:AvalonEdit 编辑器(C# 高亮 + Ctrl 滚轮字号 + JetBrains/Viora 配色),
/// minimap 交互(点击跳转 / 滚轮滚动),折叠状态,编译徽章,Before/After 对比裁剪,输出 Tab。
/// </summary>
public partial class WorkshopPage : UserControl
{
    private System.Windows.Controls.ScrollViewer? _editorScroll;

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

        if (vm.MinimapDocument is not null) MinimapBox.Document = vm.MinimapDocument;

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
                case nameof(WorkshopViewModel.MinimapDocument) when vm.MinimapDocument is not null:
                    MinimapBox.Document = vm.MinimapDocument;
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
                    MinimapColumn.Width = vm.ShowMinimap ? new GridLength(56) : new GridLength(30);
                    MinimapHost.Visibility = vm.ShowMinimap ? Visibility.Visible : Visibility.Collapsed;
                    MinimapExpand.Visibility = vm.ShowMinimap ? Visibility.Collapsed : Visibility.Visible;
                    break;
                case nameof(WorkshopViewModel.ShowFileList):
                    FileListColumn.Width = vm.ShowFileList ? new GridLength(150) : new GridLength(30);
                    FileListHost.Visibility = vm.ShowFileList ? Visibility.Visible : Visibility.Collapsed;
                    FileListExpand.Visibility = vm.ShowFileList ? Visibility.Collapsed : Visibility.Visible;
                    break;
            }
        };
        UpdateBadge(vm.CompileState);
        Loaded += (_, _) =>
        {
            ApplyHighlightPalette(vm.JetBrainsHighlight);
            // minimap 视口指示随编辑器滚动更新
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

    // ---------- Minimap 交互:点击跳转 / 滚轮滚动 ----------

    private void Minimap_MouseDown(object sender, MouseButtonEventArgs e)
    {
        JumpEditorToMinimap(e.GetPosition(MinimapBox).Y);
        e.Handled = true;
    }

    private void Minimap_MouseWheel(object sender, MouseWheelEventArgs e)
    {
        _editorScroll ??= FindScrollViewer(Editor);
        if (_editorScroll is null) return;
        _editorScroll.ScrollToVerticalOffset(_editorScroll.VerticalOffset - e.Delta);
        e.Handled = true;
    }

    private void JumpEditorToMinimap(double yInMinimap)
    {
        _editorScroll ??= FindScrollViewer(Editor);
        var miniScroll = FindScrollViewer(MinimapBox);
        if (_editorScroll is null || miniScroll is null) return;
        if (miniScroll.ExtentHeight <= 0) return;
        double ratio = yInMinimap / miniScroll.ExtentHeight;
        _editorScroll.ScrollToVerticalOffset(ratio * _editorScroll.ExtentHeight - _editorScroll.ViewportHeight / 2);
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

    private System.Windows.Controls.ScrollViewer? FindScrollViewer(System.Windows.DependencyObject root)
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

    // ---------- 编辑器:文本回写 VM ----------

    private void Editor_OnTextChanged(object sender, EventArgs e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if (string.Equals(Editor.Text, vm.SourceCode, StringComparison.Ordinal)) return;
        vm.SourceCode = Editor.Text;
    }

    // ---------- 文件重命名:双击名字进入编辑,Enter 提交 / Esc 取消 / 失焦提交 ----------

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

    private void Rename_OnClick(object sender, RoutedEventArgs e)
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

    // ---------- 预览参数调节 ----------

    private void ParamSlider_OnValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if ((sender as FrameworkElement)?.DataContext is WorkshopViewModel.PreviewParam p)
            vm.SetPreviewParam(p.Key, e.NewValue);
    }

    // ---------- 文件重命名提交(Enter/失焦) ----------

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

    // ---------- 编译状态徽章 ----------

    /// <summary>minimap 视口指示:半透明矩形标出编辑器可见区域(按滚动比例)。</summary>
    private void UpdateMinimapViewport()
    {
        var miniScroll = FindScrollViewer(MinimapBox);
        if (_editorScroll is null || miniScroll is null) return;
        if (_editorScroll.ExtentHeight <= 0 || miniScroll.ExtentHeight <= 0) return;

        double miniH = MinimapBox.ActualHeight;
        if (miniH <= 0) return;
        double top = Math.Clamp(_editorScroll.VerticalOffset / _editorScroll.ExtentHeight * miniScroll.ExtentHeight,
            0, miniScroll.ExtentHeight);
        MinimapViewport.Width = MinimapBox.ActualWidth;
        MinimapViewport.Height = Math.Max(4,
            Math.Min(1.0, _editorScroll.ViewportHeight / _editorScroll.ExtentHeight) * miniScroll.ExtentHeight);
        System.Windows.Controls.Canvas.SetLeft(MinimapViewport, 0);
        System.Windows.Controls.Canvas.SetTop(MinimapViewport, top);
    }

    // ---------- 输出折叠 ----------

    private void OutputCollapse_OnChecked(object sender, RoutedEventArgs e)
    {
        // XAML 解析时 IsChecked="True" 会在 OutputBorder 创建前触发 Checked,须判空
        if (OutputBorder is null) return;
        OutputBorder.Visibility = OutputCollapse.IsChecked == true ? Visibility.Visible : Visibility.Collapsed;
    }

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
        var tab = (sender as RadioButton)?.Tag?.ToString() ?? "code";
        if (DataContext is WorkshopViewModel vm)
            vm.SwitchTabCommand.Execute(tab);
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
