using System.Windows;
using System.Windows.Controls;
using System.Windows.Media.Animation;
using Viora.UI.Theming;

namespace Viora.UI.Shell;

/// <summary>
/// Shell chrome: responsive sidebar (rail ⇄ full; width snapped instantly to avoid
/// animation/label race), page enter transition (fade + slide), rail tooltips.
/// </summary>
public partial class ShellView : UserControl
{
    private object? _lastPage;

    public ShellView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
    }

    private ShellViewModel? ViewModel => DataContext as ShellViewModel;

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
