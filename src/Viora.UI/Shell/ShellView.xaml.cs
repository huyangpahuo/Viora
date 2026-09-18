using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;

namespace Viora.UI.Shell;

/// <summary>
/// Shell chrome: page enter transition (fade + slide) and maximized-frame compensation
/// (WindowChrome bleeds past monitor edges by the resize border when maximized).
/// </summary>
public partial class ShellView : UserControl
{
    /// <summary>Inset applied to the whole shell while the window is maximized.</summary>
    private const double MaximizedInset = 7;

    private object? _lastPage;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += (_, _) => HookWindow();
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

    private void HookWindow()
    {
        var win = Window.GetWindow(this);
        if (win is null) return;
        win.StateChanged += (_, _) =>
        {
            Root.Margin = win.WindowState == WindowState.Maximized
                ? new Thickness(MaximizedInset)
                : new Thickness(0);
            ViewModel?.RefreshWindowState();
        };
    }

    private void OnDataContextChanged(object sender, DependencyPropertyChangedEventArgs e)
    {
        if (e.OldValue is ShellViewModel old) old.PropertyChanged -= OnViewModelPropertyChanged;
        if (e.NewValue is ShellViewModel vm) vm.PropertyChanged += OnViewModelPropertyChanged;
    }

    private void OnViewModelPropertyChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(ShellViewModel.CurrentPage)) PlayPageTransition();
    }

    private void PlayPageTransition()
    {
        if (PageHost.Content == _lastPage) return;
        _lastPage = PageHost.Content;

        var fade = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(200))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        PageHost.BeginAnimation(OpacityProperty, fade);

        var slide = new ThicknessAnimation(new Thickness(0, 10, 0, 0), new Thickness(0), TimeSpan.FromMilliseconds(220))
        {
            EasingFunction = new CubicEase { EasingMode = EasingMode.EaseOut },
        };
        PageHost.BeginAnimation(MarginProperty, slide);
    }
}
