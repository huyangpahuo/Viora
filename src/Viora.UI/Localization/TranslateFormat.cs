using System.Windows;
using System.Windows.Markup;

namespace Viora.UI.Localization;

/// <summary>XAML helpers for format strings: Text="{loc:TranslateFormat Key=About.Version, V={x:Static ...}}".
/// Simpler: a StringFormat attached property pair used by pages needing {0} interpolation.</summary>
public static class TranslateFormat
{
    public static readonly DependencyProperty KeyProperty = DependencyProperty.RegisterAttached(
        "Key", typeof(string), typeof(TranslateFormat), new PropertyMetadata(string.Empty));

    public static readonly DependencyProperty ValueProperty = DependencyProperty.RegisterAttached(
        "Value", typeof(object), typeof(TranslateFormat), new PropertyMetadata(null, OnChanged));

    public static readonly DependencyProperty ResultProperty = DependencyProperty.RegisterAttached(
        "Result", typeof(string), typeof(TranslateFormat), new PropertyMetadata(string.Empty));

    public static string GetKey(DependencyObject obj) => (string)obj.GetValue(KeyProperty);
    public static void SetKey(DependencyObject obj, string value) => obj.SetValue(KeyProperty, value);

    public static object? GetValue(DependencyObject obj) => obj.GetValue(ValueProperty);
    public static void SetValue(DependencyObject obj, object? value) => obj.SetValue(ValueProperty, value);

    public static string GetResult(DependencyObject obj) => (string)obj.GetValue(ResultProperty);
    public static void SetResult(DependencyObject obj, string value) => obj.SetValue(ResultProperty, value);

    private static void OnChanged(DependencyObject d, DependencyPropertyChangedEventArgs e) => Update(d);

    private static void Update(DependencyObject d)
    {
        string key = GetKey(d);
        if (key.Length == 0) return;
        string template = LocalizationSource.Current[key];
        object? value = GetValue(d);
        SetResult(d, string.Format(template, value ?? string.Empty));
    }
}

/// <summary>Simple static helper for code-behind/VM string formatting.</summary>
public static class Tr
{
    public static string Get(string key) => LocalizationSource.Current[key];

    public static string Format(string key, object arg0) => string.Format(Get(key), arg0);

    public static string Format(string key, object arg0, object arg1) => string.Format(Get(key), arg0, arg1);
}
