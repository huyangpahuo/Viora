using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Viora.UI.Controls;

/// <summary>
/// Settings-style checkbox row: label + optional description under it.
/// Styled in Themes/Controls.xaml (VioraCheckBox).
/// </summary>
public sealed class FormCheckBox : Control
{
    public static readonly DependencyProperty IsCheckedProperty = DependencyProperty.Register(
        nameof(IsChecked), typeof(bool?), typeof(FormCheckBox),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

    public static readonly DependencyProperty LabelProperty = DependencyProperty.Register(
        nameof(Label), typeof(string), typeof(FormCheckBox), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(FormCheckBox), new PropertyMetadata(string.Empty));

    public bool? IsChecked
    {
        get => (bool?)GetValue(IsCheckedProperty);
        set => SetValue(IsCheckedProperty, value);
    }

    public string Label
    {
        get => (string)GetValue(LabelProperty);
        set => SetValue(LabelProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    static FormCheckBox() =>
        DefaultStyleKeyProperty.OverrideMetadata(typeof(FormCheckBox),
            new FrameworkPropertyMetadata(typeof(FormCheckBox)));
}
