using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Viora.UI.Pages.Stylize;

namespace Viora.UI.Pages.Stylize;

/// <summary>
/// 风格化工作台 code-behind:before/after 分割拖拽、缩放平移手势、多文件拖入。
/// 分割线位于 CompareRoot(精确贴合图片内容区的坐标空间),布局在每次 LayoutUpdated
/// 后重算,页面过渡/侧栏变化不会留下过期矩形。
/// </summary>
public partial class StylizePage : UserControl
{
    /// <summary>视口内边距(图片四周留白)。</summary>
    private const double ViewportPadding = 16;

    /// <summary>内容盒上限 — 导入图缩进这个盒子内,不充满整个视口。</summary>
    private const double MaxContentWidth = 980;
    private const double MaxContentHeight = 620;

    private const double SplitGrabRadius = 18;

    private enum DragKind { None, Split, Pan }

    private DragKind _drag;
    private Point _panOrigin;
    private double _panStartX, _panStartY;
    private StylizeViewModel? _wiredViewModel;

    public StylizePage(StylizeViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        BuildHotkeys(vm);
        Loaded += (_, _) => WireViewModel();
        SizeChanged += (_, _) => UpdateSplitLayout();
        LayoutUpdated += (_, _) => UpdateSplitLayout();
    }

    private StylizeViewModel ViewModel => (StylizeViewModel)DataContext;

    /// <summary>缩略图条内部 ScrollViewer(横向滚动宿主),惰性解析。</summary>
    private ScrollViewer? _thumbsScroll;

    private ScrollViewer? _chipsScroll;

    private void WireViewModel()
    {
        if (_wiredViewModel == ViewModel) return;
        _wiredViewModel = ViewModel;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(StylizeViewModel.SplitPosition)
                or nameof(StylizeViewModel.OriginalImageSource)
                or nameof(StylizeViewModel.ResultImageSource)
                or nameof(StylizeViewModel.CompareMode))
            {
                UpdateSplitLayout();
            }
            else if (e.PropertyName is nameof(StylizeViewModel.SelectedItem) or nameof(StylizeViewModel.HasImage))
            {
                // 选中/插入新图 → 缩略图条自动滚到该图
                if (ViewModel.SelectedItem is not null)
                    ThumbsList.ScrollIntoView(ViewModel.SelectedItem);
            }
        };

        ViewModel.Items.CollectionChanged += (_, e) =>
        {
            if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add && e.NewItems is not null)
                foreach (var item in e.NewItems)
                    ThumbsList.ScrollIntoView(item);
        };
    }

    /// <summary>缩略图条滚轮 → 横向滚动(每次插入新图也会跟随,见 WireViewModel)。</summary>
    /// <summary>页面快捷键(开始风格化/打开图片):按服务配置生成 KeyBinding。</summary>
    private void BuildHotkeys(StylizeViewModel vm)
    {
        var hotkeys = vm.Hotkeys;
        void Bind(string id, System.Windows.Input.ICommand command)
        {
            var gesture = Viora.UI.Services.HotkeyService.ToKeyGesture(hotkeys.GetGesture(id));
            if (gesture is not null)
                InputBindings.Add(new System.Windows.Input.KeyBinding(command, gesture));
        }
        Bind("stylize.run", vm.RunCommand);
        Bind("stylize.open", vm.ImportCommand);
    }

    private void OnChipsWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ListBox lb)
        {
            _chipsScroll ??= FindDescendantScrollViewer(lb);
            _chipsScroll.ScrollToHorizontalOffset(_chipsScroll.HorizontalOffset - e.Delta * 0.6);
            e.Handled = true;
        }
    }

    private void OnThumbsWheel(object sender, MouseWheelEventArgs e)
    {
        if (_thumbsScroll is null) _thumbsScroll = FindDescendantScrollViewer(ThumbsList);
        if (_thumbsScroll is null) return;
        _thumbsScroll.ScrollToHorizontalOffset(_thumbsScroll.HorizontalOffset - e.Delta * 0.6);
        e.Handled = true;
    }

    /// <summary>历史记录条滚轮 → 横向滚动。</summary>
    private void OnHistoryWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToHorizontalOffset(sv.HorizontalOffset - e.Delta * 0.3);
            e.Handled = true;
        }
    }

    private static ScrollViewer? FindDescendantScrollViewer(DependencyObject root)
    {
        int count = VisualTreeHelper.GetChildrenCount(root);
        for (int i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);
            if (child is ScrollViewer sv) return sv;
            var found = FindDescendantScrollViewer(child);
            if (found is not null) return found;
        }
        return null;
    }

    /// <summary>批量面板拖拽入队。</summary>
    private void OnBatchDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _ = ViewModel.AddBatchFilesAsync(files);
    }

    // ---------- 分割布局 ----------

    private void UpdateSplitLayout()
    {
        UpdateContentRect();
        UpdateClip();
        UpdateSliderVisual();
    }

    private void UpdateContentRect()
    {
        if (CompareRoot is null || ViewerBorder is null) return;
        var img = ViewModel.OriginalImageSource;
        double availW = Math.Min(ViewerBorder.ActualWidth - 2 * ViewportPadding, MaxContentWidth);
        double availH = Math.Min(ViewerBorder.ActualHeight - 2 * ViewportPadding, MaxContentHeight);
        if (img is null || availW <= 8 || availH <= 8) return;

        double imgW = img.Width, imgH = img.Height;
        if (imgW <= 0 || imgH <= 0 || double.IsNaN(imgW) || double.IsNaN(imgH)) return;

        double scale = Math.Min(availW / imgW, availH / imgH);
        double w = Math.Round(imgW * scale, 1);
        double h = Math.Round(imgH * scale, 1);

        if (Math.Abs(CompareRoot.Width - w) > 0.5) CompareRoot.Width = w;
        if (Math.Abs(CompareRoot.Height - h) > 0.5) CompareRoot.Height = h;
    }

    private bool TryGetContentSize(out double width, out double height)
    {
        width = CompareRoot.ActualWidth > 0 ? CompareRoot.ActualWidth : CompareRoot.Width;
        height = CompareRoot.ActualHeight > 0 ? CompareRoot.ActualHeight : CompareRoot.Height;
        return width > 0 && height > 0;
    }

    private void UpdateClip()
    {
        if (ResultClip is null || CompareRoot is null) return;
        if (!TryGetContentSize(out double w, out double h)) return;

        double x = w * ViewModel.SplitPosition;
        ResultClip.Rect = new Rect(x, 0, Math.Max(0, w - x), h);
    }

    private void UpdateSliderVisual()
    {
        if (SplitOverlay is null || ActualWidth <= 0) return;
        if (!TryGetContentSize(out double w, out double h)) return;

        double x = w * ViewModel.SplitPosition;

        if (DividerLine is not null)
        {
            DividerLine.Height = h;
            Canvas.SetLeft(DividerLine, x - DividerLine.Width / 2);
            Canvas.SetTop(DividerLine, 0);
        }

        if (DividerGrip is not null)
        {
            Canvas.SetLeft(DividerGrip, x - DividerGrip.Width / 2);
            Canvas.SetTop(DividerGrip, h / 2 - DividerGrip.Height / 2);
        }
    }

    // ---------- 手势 ----------

    private void OnSplitDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.ShowCompareSplit) return;
        if (TryGetContentSize(out double w, out _) && IsNearDivider(e, w))
        {
            _drag = DragKind.Split;
            SplitHandle.CaptureMouse();
            MoveSplitTo(e);
        }
        else
        {
            BeginPan(e);
        }
        e.Handled = true;
    }

    private void OnSplitMove(object sender, MouseEventArgs e)
    {
        if (_drag == DragKind.Split) MoveSplitTo(e);
        else if (_drag == DragKind.Pan) ApplyPan(e);
    }

    private void OnSplitUp(object sender, MouseButtonEventArgs e) => EndDrag();

    /// <summary>右键(或双击)分割区 → 分割线回中。</summary>
    private void OnDoubleResetSplit(object sender, MouseButtonEventArgs e)
    {
        if (ViewModel.ShowCompareSplit) ViewModel.ResetSplitCommand.Execute(null);
    }

    /// <summary>风格卡片列表滚轮:外层 ScrollViewer 统一滚动(内层禁滚,事件被截获)。</summary>
    private void OnListWheel(object sender, MouseWheelEventArgs e)
    {
        if (sender is ScrollViewer sv)
        {
            sv.ScrollToVerticalOffset(sv.VerticalOffset - e.Delta * 0.6);
            e.Handled = true;
        }
    }

    private void OnViewportWheel(object sender, MouseWheelEventArgs e)
    {
        if (!ViewModel.HasImage) return;
        double zoom = e.Delta > 0 ? ViewModel.Zoom * 1.15 : ViewModel.Zoom / 1.15;
        ViewModel.Zoom = Math.Clamp(zoom, 1.0, 6.0);
        e.Handled = true;
    }

    private bool IsNearDivider(MouseEventArgs e, double contentWidth)
    {
        var pos = e.GetPosition(CompareRoot);
        double dividerX = contentWidth * ViewModel.SplitPosition;
        return Math.Abs(pos.X - dividerX) <= SplitGrabRadius / Math.Max(1, ViewModel.Zoom);
    }

    private void MoveSplitTo(MouseEventArgs e)
    {
        if (!TryGetContentSize(out double w, out _)) return;
        var pos = e.GetPosition(CompareRoot);
        ViewModel.SplitPosition = Math.Clamp(pos.X / w, 0, 1);
    }

    private void BeginPan(MouseEventArgs e)
    {
        _drag = DragKind.Pan;
        _panOrigin = e.GetPosition(ViewerBorder);
        _panStartX = ViewModel.PanX;
        _panStartY = ViewModel.PanY;
        SplitHandle.CaptureMouse();
    }

    private void ApplyPan(MouseEventArgs e)
    {
        if (!ViewModel.Zoomed) return;
        var pos = e.GetPosition(ViewerBorder);
        double dx = pos.X - _panOrigin.X;
        double dy = pos.Y - _panOrigin.Y;

        double maxX = (ViewModel.Zoom - 1) / 2 * ViewerBorder.ActualWidth;
        double maxY = (ViewModel.Zoom - 1) / 2 * ViewerBorder.ActualHeight;

        ViewModel.PanX = Math.Clamp(_panStartX + dx, -maxX, maxX);
        ViewModel.PanY = Math.Clamp(_panStartY + dy, -maxY, maxY);
    }

    private void EndDrag()
    {
        _drag = DragKind.None;
        if (SplitHandle.IsMouseCaptured) SplitHandle.ReleaseMouseCapture();
    }

    // ---------- 拖放导入 ----------

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            _ = ViewModel.ImportFilesCommand.ExecuteAsync(files);
    }
}
