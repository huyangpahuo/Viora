using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Viora.UI.Services;

namespace Viora.UI.Pages.Convert;

/// <summary>
/// Convert page code-behind: Before/After comparison slider (visible divider +
/// grip driven from SplitPosition) and the responsive reflow — below 1000px the
/// parameter panel stacks under the viewport.
/// </summary>
public partial class ConvertPage : UserControl
{
    private const double NarrowLayoutWidth = 1000;

    private bool _splitDragging;
    private ConvertViewModel? _wiredViewModel;

    public ConvertPage(ConvertViewModel vm)
    {
        DataContext = vm;
        InitializeComponent();
        Loaded += (_, _) => { WireSplit(); UpdateNarrowState(); UpdateClip(); UpdateSliderVisual(); };
        SizeChanged += (_, _) => { UpdateNarrowState(); UpdateClip(); UpdateSliderVisual(); };
        // Any layout pass (page transition margin animation, sidebar toggle, panel
        // reflow) re-syncs clip + slider — stale rectangles are impossible.
        LayoutUpdated += (_, _) => { UpdateClip(); UpdateSliderVisual(); };
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
                or nameof(ConvertViewModel.ResultImageSource)
                or nameof(ConvertViewModel.CompareMode))
            {
                UpdateClip();
                UpdateSliderVisual();
            }
        };
    }

    private void UpdateClip()
    {
        if (ResultClip is null || SplitOverlay is null) return;
        double viewportWidth = SplitOverlay.ActualWidth;
        double viewportHeight = SplitOverlay.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        double x = viewportWidth * ViewModel.SplitPosition;
        ResultClip.Rect = new Rect(x, 0, Math.Max(0, viewportWidth - x), viewportHeight);
    }

    /// <summary>Positions divider line + grip from SplitPosition.</summary>
    private void UpdateSliderVisual()
    {
        if (SplitOverlay is null || ActualWidth <= 0) return;
        double viewportWidth = SplitOverlay.ActualWidth;
        double viewportHeight = SplitOverlay.ActualHeight;
        if (viewportWidth <= 0 || viewportHeight <= 0) return;

        double x = viewportWidth * ViewModel.SplitPosition;

        if (DividerLine is not null)
        {
            DividerLine.Height = viewportHeight;
            Canvas.SetLeft(DividerLine, x - DividerLine.Width / 2);
            Canvas.SetTop(DividerLine, 0);
        }

        if (DividerGrip is not null)
        {
            Canvas.SetLeft(DividerGrip, x - DividerGrip.Width / 2);
            Canvas.SetTop(DividerGrip, viewportHeight / 2 - DividerGrip.Height / 2);
        }
    }

    private void OnSplitDown(object sender, MouseButtonEventArgs e) => _splitDragging = true;

    private void OnSplitMove(object sender, MouseEventArgs e)
    {
        if (!_splitDragging) return;
        var pos = e.GetPosition(OriginalHost);
        ViewModel.SplitPosition = Math.Clamp(pos.X / Math.Max(1, OriginalHost.ActualWidth), 0, 1);
    }

    private void OnSplitUp(object sender, MouseButtonEventArgs e) => _splitDragging = false;

    private void OnDragOver(object sender, DragEventArgs e) =>
        e.Effects = e.Data.GetDataPresent(DataFormats.FileDrop) ? DragDropEffects.Copy : DragDropEffects.None;

    private async void OnDrop(object sender, DragEventArgs e)
    {
        if (e.Data.GetData(DataFormats.FileDrop) is string[] files && files.Length > 0)
            await ViewModel.ImportCommand.ExecuteAsync(files[0]);
    }
}
