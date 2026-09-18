using System.Windows;
using System.Windows.Controls;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Placeholder;

/// <summary>
/// 通用空白占位页:重构期间未实现的导航目标共享这一个类,
/// 标题/副标题由 Shell 注入,语言切换实时刷新。
/// </summary>
public partial class PlaceholderPage : UserControl
{
    public static readonly DependencyProperty TitleKeyProperty = DependencyProperty.Register(
        nameof(TitleKey), typeof(string), typeof(PlaceholderPage), new PropertyMetadata(string.Empty, (_, _) => RefreshStatic()));

    public static readonly DependencyProperty SubtitleKeyProperty = DependencyProperty.Register(
        nameof(SubtitleKey), typeof(string), typeof(PlaceholderPage), new PropertyMetadata(null, (_, _) => RefreshStatic()));

    private static readonly List<PlaceholderPage> _instances = new();

    public PlaceholderPage()
    {
        InitializeComponent();
        _instances.Add(this);
        LocalizationSource.Current.PropertyChanged += (_, _) => RefreshStatic();
        Loaded += (_, _) => RefreshStatic();
    }

    public string TitleKey
    {
        get => (string)GetValue(TitleKeyProperty);
        set => SetValue(TitleKeyProperty, value);
    }

    public string? SubtitleKey
    {
        get => (string?)GetValue(SubtitleKeyProperty);
        set => SetValue(SubtitleKeyProperty, value);
    }

    private static void RefreshStatic()
    {
        foreach (var page in _instances)
        {
            page.TitleText.Text = Tr.Get(page.TitleKey);
            page.SubtitleText.Text = page.SubtitleKey is null ? string.Empty : Tr.Get(page.SubtitleKey);
            page.SubtitleText.Visibility = page.SubtitleKey is null ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
