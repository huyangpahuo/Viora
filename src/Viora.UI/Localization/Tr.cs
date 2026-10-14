namespace Viora.UI.Localization;

/// <summary>Simple static helper for code-behind/VM string formatting.</summary>
public static class Tr
{
    public static string Get(string key) => LocalizationSource.Current[key];

    public static string Format(string key, object arg0) => string.Format(Get(key), arg0);

    public static string Format(string key, object arg0, object arg1) => string.Format(Get(key), arg0, arg1);
}
