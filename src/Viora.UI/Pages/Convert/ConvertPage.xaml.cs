using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Viora.UI.Services;

namespace Viora.UI.Pages.Convert;

/// <summary>
/// Convert page code-behind: viewport gestures (before/after split drag near the divider,
/// free pan when zoomed, wheel zoom), the split layout math and the responsive reflow —
/// below 1000px the parameter panel stacks under the viewport.
///
/// The split lives inside CompareRoot, sized to the exact letterboxed image bounds
/// ("content rect"), so divider, clip and drag math share one coordinate space; using the
/// viewport instead would drift by the image margin and by Uniform-stretch letterboxing.
/// </summary>
public partial class ConvertPage : UserControl
{
    private const double NarrowLayoutWidth = 1000;

    /// <summary>Inner padding of the viewport (kept as breathing room around the content box).</summary>
    private const double ViewportPadding = 16;

    /// <summary>Upper bound of the fixed content box — imported images shrink to fit inside it.</summary>
    private const double MaxContentWidth = 560;
    private const double MaxContentHeight = 460;

    private const double SplitGrabRadius = 18;

    private enum DragKind { None, Split, Pan }

    private DragKind _drag;
    private Point _panOrigin;
    private double _panStartX, _panStartY;
    private ConvertViewModel? _wiredViewModel;

    public ConvertPage(ConvertViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += (_, _) => { WireSplit(); UpdateNarrowState(); UpdateSplitLayout(); };
        SizeChanged += (_, _) => { UpdateNarrowState(); UpdateSplitLayout(); };
        // Any layout pass (page transition margin animation, sidebar toggle, panel
        // reflow) re-syncs content rect + split — stale rectangles are impossible.
        LayoutUpdated += (_, _) => UpdateSplitLayout();
    }

    private ConvertViewModel ViewModel => (ConvertViewModel)DataContext;

    /// <summary>Responsive: below 1000px stack the panel under the viewport.</summary>
    private void UpdateNarrowState()
    {
        if (ActualWidth <= 0) return;
        bool narrow = ActualWidth < NarrowLayoutWidth;

        if (narrow)
        {
            Grid.SetRow(ViewportBorder, 1);
            Grid.SetRowSpan(ViewportBorder, 1);
            Grid.SetColumn(ViewportBorder, 0);
            Grid.SetColumnSpan(ViewportBorder, 3);
            ViewportBorder.Height = 420;

            Grid.SetRow(PanelScroll, 2);
            Grid.SetRowSpan(PanelScroll, 1);
            Grid.SetColumn(PanelScroll, 0);
            Grid.SetColumnSpan(PanelScroll, 3);
        }
        else
        {
            ViewportBorder.Height = double.NaN;
            Grid.SetRow(ViewportBorder, 0);
            Grid.SetRowSpan(ViewportBorder, 2);
            Grid.SetColumn(ViewportBorder, 0);
            Grid.SetColumnSpan(ViewportBorder, 1);

            Grid.SetRow(PanelScroll, 0);
            Grid.SetRowSpan(PanelScroll, 3);
            Grid.SetColumn(PanelScroll, 2);
            Grid.SetColumnSpan(PanelScroll, 1);
        }
    }

    private void WireSplit()
    {
        if (_wiredViewModel == ViewModel) return;
        _wiredViewModel = ViewModel;

        ViewModel.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName is nameof(ConvertViewModel.SplitPosition)
                or nameof(ConvertViewModel.OriginalImageSource)
                or nameof(ConvertViewModel.ResultImageSource)
                or nameof(ConvertViewModel.CompareMode))
            {
                UpdateSplitLayout();
            }
        };
    }

    /// <summary>Recomputes the content rect, then re-derives clip + divider from it.</summary>
    private void UpdateSplitLayout()
    {
        UpdateContentRect();
        UpdateClip();
        UpdateSliderVisual();
    }

    /// <summary>
    /// Sizes CompareRoot to the largest rectangle of the original image's aspect that fits
    /// the viewport AND the fixed content box (MaxContent*), so imported images shrink into
    /// a contained window instead of flooding the viewport. The epsilon guard keeps the
    /// LayoutUpdated hook from re-triggering layout forever.
    /// </summary>
    private void UpdateContentRect()
    {
        if (CompareRoot is null || ViewportBorder is null) return;
        var img = ViewModel.OriginalImageSource;
        double availW = Math.Min(ViewportBorder.ActualWidth - 2 * ViewportPadding, MaxContentWidth);
        double availH = Math.Min(ViewportBorder.ActualHeight - 2 * ViewportPadding, MaxContentHeight);
        if (img is null || availW <= 8 || availH <= 8) return;

        double imgW = img.Width, imgH = img.Height;
        if (imgW <= 0 || imgH <= 0 || double.IsNaN(imgW) || double.IsNaN(imgH)) return;

        double scale = Math.Min(availW / imgW, availH / imgH);
        double w = Math.Round(imgW * scale, 1);
        double h = Math.Round(imgH * scale, 1);

        if (Math.Abs(CompareRoot.Width - w) > 0.5) CompareRoot.Width = w;
        if (Math.Abs(CompareRoot.Height - h) > 0.5) CompareRoot.Height = h;
    }

    /// <summary>Content-rect size after layout, falling back to the explicitly set values.</summary>
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

    /// <summary>Positions divider line + grip inside the content rect.</summary>
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

    // ---------- Gestures ----------

    /// <summary>Pan on the viewport itself (original / result / side-by-side modes).</summary>
    private void OnViewportMouseDown(object sender, MouseButtonEventArgs e)
    {
        if (!ViewModel.HasImage) return;
        BeginPan(e, ViewportBorder);
    }

    private void OnViewportMouseMove(object sender, MouseEventArgs e)
    {
        if (_drag == DragKind.Pan) ApplyPan(e);
    }

    private void OnViewportMouseUp(object sender, MouseButtonEventArgs e) => EndDrag();

    /// <summary>
    /// Split-mode input surface: mouse down near the divider starts a split drag,
    /// anywhere else starts a pan (no-op at fit zoom).
    /// </summary>
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
            BeginPan(e, SplitHandle);
        }
        e.Handled = true;
    }

    private void OnSplitMove(object sender, MouseEventArgs e)
    {
        if (_drag == DragKind.Split) MoveSplitTo(e);
        else if (_drag == DragKind.Pan) ApplyPan(e);
    }

    private void OnSplitUp(object sender, MouseButtonEventArgs e) => EndDrag();

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
        // Screen-space grab radius shrinks in content coordinates when zoomed in.
        return Math.Abs(pos.X - dividerX) <= SplitGrabRadius / Math.Max(1, ViewModel.Zoom);
    }

    private void MoveSplitTo(MouseEventArgs e)
    {
        if (!TryGetContentSize(out double w, out _)) return;
        var pos = e.GetPosition(CompareRoot);
        ViewModel.SplitPosition = Math.Clamp(pos.X / w, 0, 1);
    }

    private void BeginPan(MouseEventArgs e, IInputElement captureTarget)
    {
        _drag = DragKind.Pan;
        _panOrigin = e.GetPosition(ViewportBorder);
        _panStartX = ViewModel.PanX;
        _panStartY = ViewModel.PanY;
        captureTarget.CaptureMouse();
    }

    private void ApplyPan(MouseEventArgs e)
    {
        if (!ViewModel.Zoomed) return;
        var pos = e.GetPosition(ViewportBorder);
        double dx = pos.X - _panOrigin.X;
        double dy = pos.Y - _panOrigin.Y;

        // The centered scale transform grows the content by (Zoom-1)/2 per side;
        // panning may not push the content edge past the viewport edge.
        double maxX = (ViewModel.Zoom - 1) / 2 * ViewportBorder.ActualWidth;
        double maxY = (ViewModel.Zoom - 1) / 2 * ViewportBorder.ActualHeight;

        ViewModel.PanX = Math.Clamp(_panStartX + dx, -maxX, maxX);
        ViewModel.PanY = Math.Clamp(_panStartY + dy, -maxY, maxY);
    }

    private void EndDrag()
    {
        _drag = DragKind.None;
        if (SplitHandle.IsMouseCaptured) SplitHandle.ReleaseMouseCapture();
        if (ViewportBorder.IsMouseCaptured) ViewportBorder.ReleaseMouseCapture();
    }

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await ViewModel.ImportCommand.ExecuteAsync(files[0]);
    }
}
