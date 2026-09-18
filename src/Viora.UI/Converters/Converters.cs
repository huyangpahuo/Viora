using System.Globalization;
using System.Windows;
using System.Windows.Data;
using Viora.UI.Localization;

namespace Viora.UI.Converters;

public sealed class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Visible;
}

public sealed class InverseBoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is true ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is Visibility.Collapsed;
}

public sealed class InverseBoolConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool b ? !b : value;
}

/// <summary>Null/empty string → Collapsed (used for optional subtitles).</summary>
public sealed class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrWhiteSpace(value as string) ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bool (false = visible) — for "hide when failed" style cases.</summary>
public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is int count && count > 0 ? Visibility.Collapsed : Visibility.Visible;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Slider fill width = (value - min) / (max - min) * trackWidth. Registered as
/// {x:Static conv:Converters.SliderFill} inside control templates.
/// </summary>
public sealed class SliderFillWidthConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        if (values.Length == 4
            && values[0] is double value
            && values[1] is double min
            && values[2] is double max
            && values[3] is double trackWidth)
        {
            double range = max - min;
            if (range <= 0) return 0.0;
            return Math.Clamp((value - min) / range, 0, 1) * trackWidth;
        }
        return 0.0;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Bool → sidebar width (compact rail vs full).</summary>
public sealed class SidebarWidthConverter : IValueConverter
{
    public double ExpandedWidth { get; set; } = 248;

    public double CompactWidth { get; set; } = 56;

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is bool compact && compact ? CompactWidth : ExpandedWidth;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Window width → responsive horizontal gutter thickness.</summary>
public sealed class ResponsiveGutterConverter : IValueConverter
{
    public double Wide { get; set; } = 36;

    public double Narrow { get; set; } = 16;

    public double Threshold { get; set; } = 960;

    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is double w && w < Threshold
            ? new Thickness(Narrow, 24, Narrow, 0)
            : new Thickness(Wide, 24, Wide, 0);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Localization key (string) → translated text. For dynamic keys (group headers).</summary>
public sealed class TranslateKeyConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        Tr.Get(value as string ?? string.Empty);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Icon key (e.g. "Gear") → geometry from the generated icon library (Assets/Icons.xaml,
/// key "Icon." + name). View models stay string-keyed so they never touch WPF resources.
/// </summary>
public sealed class IconKeyConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is string key
            ? System.Windows.Application.Current.TryFindResource("Icon." + key)
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>ImageSource → UniformToFill ImageBrush. Rounded Borders must paint the
/// image as their own background — WPF's ClipToBounds does not clip children to
/// CornerRadius, so a raw Image child would poke square corners out of the frame.</summary>
public sealed class ImageToBrushConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        value is System.Windows.Media.ImageSource source
            ? new System.Windows.Media.ImageBrush(source) { Stretch = System.Windows.Media.Stretch.UniformToFill }
            : null;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>value.ToString() == parameter → Visible(批量队列状态图标)。</summary>
public sealed class EqualsToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal)
            ? Visibility.Visible
            : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>空字符串 → Visible(TextBox 占位文本)。</summary>
public sealed class StringEmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.IsNullOrEmpty(value as string) ? Visibility.Visible : Visibility.Collapsed;

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>Language code (value) → display name ("中文" / "English").</summary>
public sealed class LanguageDisplayNameConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        Viora.UI.Shell.ShellViewModel.LanguageDisplayName(value as string ?? string.Empty);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// Language code (value) == display name (parameter, the current language) — drives the
/// check mark on language menu items.
/// </summary>
public sealed class LanguageEqualsCurrentConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(
            Viora.UI.Shell.ShellViewModel.LanguageDisplayName(value as string ?? string.Empty),
            parameter as string,
            StringComparison.Ordinal);

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) =>
        throw new NotSupportedException();
}

/// <summary>
/// value.ToString() == parameter.ToString() → bool. ConvertBack: true → Enum.Parse(targetType,
/// parameter) — lets a group of RadioButtons bind one enum (compare mode switcher).
/// </summary>
public sealed class EqualityConverter : IValueConverter
{
    public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) =>
        string.Equals(value?.ToString(), parameter?.ToString(), StringComparison.Ordinal);

    public object ConvertBack(object? value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is true && parameter is string s && targetType.IsEnum)
            return Enum.Parse(targetType, s);
        return Binding.DoNothing;
    }
}

public static class Converters
{
    public static readonly SliderFillWidthConverter SliderFill = new();
}
