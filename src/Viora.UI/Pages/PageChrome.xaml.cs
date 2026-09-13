using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Pages;

/// <summary>
/// Reusable page chrome: title + subtitle header and a scrollable content host.
/// Pages compose it with TitleKey/SubtitleKey + Content. Localized header refreshes live.
/// </summary>
[ContentProperty(nameof(Content))]
public sealed class PageChrome : UserControl
{
    public static readonly DependencyProperty TitleKeyProperty = DependencyProperty.Register(
        nameof(TitleKey), typeof(string), typeof(PageChrome), new PropertyMetadata(string.Empty, OnKeysChanged));

    public static readonly DependencyProperty SubtitleKeyProperty = DependencyProperty.Register(
        nameof(SubtitleKey), typeof(string), typeof(PageChrome), new PropertyMetadata(string.Empty, OnKeysChanged));

    public static new readonly DependencyProperty ContentProperty = DependencyProperty.Register(
        nameof(Content), typeof(object), typeof(PageChrome), new PropertyMetadata(null));

    public static readonly DependencyProperty HeaderTextProperty = DependencyProperty.Register(
        "HeaderText", typeof(string), typeof(PageChrome), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty SubheaderTextProperty = DependencyProperty.Register(
        "SubheaderText", typeof(string), typeof(PageChrome), new PropertyMetadata(string.Empty));

    public PageChrome()
    {
        LocalizationSource.Current.PropertyChanged += (_, _) => RefreshHeader();
        Loaded += (_, _) => RefreshHeader();
    }

    public string TitleKey
    {
        get => (string)GetValue(TitleKeyProperty);
        set => SetValue(TitleKeyProperty, value);
    }

    public string SubtitleKey
    {
        get => (string)GetValue(SubtitleKeyProperty);
        set => SetValue(SubtitleKeyProperty, value);
    }

    public new object? Content
    {
        get => GetValue(ContentProperty);
        set => SetValue(ContentProperty, value);
    }

    public string HeaderText => (string)GetValue(HeaderTextProperty);

    public string SubheaderText => (string)GetValue(SubheaderTextProperty);

    private static void OnKeysChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        => ((PageChrome)d).RefreshHeader();

    private void RefreshHeader()
    {
        SetValue(HeaderTextProperty, Tr.Get(TitleKey));
        SetValue(SubheaderTextProperty, string.IsNullOrEmpty(SubtitleKey) ? string.Empty : Tr.Get(SubtitleKey));
    }
}
