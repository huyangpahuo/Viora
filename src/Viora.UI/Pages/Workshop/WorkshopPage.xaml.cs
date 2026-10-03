using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Highlighting;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Workshop;

/// <summary>
/// 插件工坊 code-behind:AvalonEdit 编辑器(自带滚动/光标/C# 高亮),
/// registry 条目 JSON 高亮展示,编译状态徽章,Before/After 圆角与分割裁剪,折叠状态。
/// </summary>
public partial class WorkshopPage : UserControl
{
    public WorkshopPage(WorkshopViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();

        // AvalonEdit:C# 高亮 + 制表符转空格
        Editor.SyntaxHighlighting = HighlightingManager.Instance.GetDefinitionByExtension(".cs");
        Editor.Options.ConvertTabsToSpaces = true;
        Editor.Options.IndentationSize = 4;
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
    }

    // ---------- 编辑器:文本回写 VM + minimap 防抖 ----------

    private System.Windows.Threading.DispatcherTimer? _minimapTimer;

    private void Editor_OnTextChanged(object sender, EventArgs e)
    {
        if (DataContext is not WorkshopViewModel vm) return;
        if (string.Equals(Editor.Text, vm.SourceCode, StringComparison.Ordinal)) return;

        vm.SourceCode = Editor.Text;

        // minimap 防抖 200ms
        _minimapTimer ??= new System.Windows.Threading.DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        _minimapTimer.Stop();
        _minimapTimer.Tick -= OnMinimapTimerTick;
        _minimapTimer.Tick += OnMinimapTimerTick;
        _minimapTimer.Start();
    }

    private void OnMinimapTimerTick(object? sender, EventArgs e)
    {
        if (sender is System.Windows.Threading.DispatcherTimer t) t.Stop();
        if (DataContext is WorkshopViewModel vm) vm.UpdateMinimap(Editor.Text);
    }

    // ---------- 文件重命名:Enter 提交 / Esc 取消 / 失焦提交 ----------

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

    private void Rename_OnClick(object sender, RoutedEventArgs e)
    {
        if ((sender as FrameworkElement)?.DataContext is WorkshopViewModel.WorkshopFile file)
        {
            file.BeginRename();
            // 聚焦刚出现的重命名框
            Dispatcher.BeginInvoke(() =>
            {
                if ((sender as FrameworkElement)?.DataContext is not WorkshopViewModel.WorkshopFile f) return;
                var container = FileList.ItemContainerGenerator.ContainerFromItem(f) as ContentPresenter;
                var box = FindDescendant<TextBox>(container);
                box?.Focus();
            }, System.Windows.Threading.DispatcherPriority.Input);
        }
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
