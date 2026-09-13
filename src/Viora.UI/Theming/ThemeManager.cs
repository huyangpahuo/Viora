using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Theming;

/// <summary>
/// A selectable color scheme: id, display, base luminance, and preview swatches
/// for the Settings scheme picker. Notifies IsSelected/LocalName for live UI.
/// </summary>
public sealed class ThemeScheme : INotifyPropertyChanged
{
    public ThemeScheme(string id, string displayNameKey, bool isDark,
        string previewBackground, string previewSurface, string previewAccent,
        string previewPrimaryContainer, string previewTextPrimary)
    {
        Id = id;
        DisplayNameKey = displayNameKey;
        IsDark = isDark;
        PreviewBackground = previewBackground;
        PreviewSurface = previewSurface;
        PreviewAccent = previewAccent;
        PreviewPrimaryContainer = previewPrimaryContainer;
        PreviewTextPrimary = previewTextPrimary;
        LocalizationSource.Current.PropertyChanged += (_, _) => OnPropertyChanged(nameof(LocalName));
    }

    public string Id { get; }

    public string DisplayNameKey { get; }

    public bool IsDark { get; }

    public string PreviewBackground { get; }

    public string PreviewSurface { get; }

    public string PreviewAccent { get; }

    public string PreviewPrimaryContainer { get; }

    public string PreviewTextPrimary { get; }

    public Brush PreviewBackgroundBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(PreviewBackground));

    public Brush PreviewSurfaceBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(PreviewSurface));

    public Brush PreviewAccentBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(PreviewAccent));

    public Brush PreviewPrimaryContainerBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(PreviewPrimaryContainer));

    public Brush PreviewTextPrimaryBrush => new SolidColorBrush((Color)ColorConverter.ConvertFromString(PreviewTextPrimary));

    public string LocalName => Tr.Get(DisplayNameKey);

    public bool IsSelected => ThemeManager.Current == Id;

    public event PropertyChangedEventHandler? PropertyChanged;

    public void Refresh() 
    {
        OnPropertyChanged(nameof(IsSelected));
        OnPropertyChanged(nameof(LocalName));
    }

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}

/// <summary>
/// Applies color-scheme palettes by swapping the palette ResourceDictionary in
/// Application.Resources. Non-color tokens (Core.xaml) are shared and never swapped.
/// </summary>
public static class ThemeManager
{
    public const string Dark = "graphite";   // legacy alias from v1 settings
    public const string Light = "paper";     // legacy alias from v1 settings

    public static string Current { get; private set; } = Dark;

    public static IReadOnlyList<ThemeScheme> Schemes { get; } = new ThemeScheme[]
    {
        //                               id         nameKey          dark  background   surface      primary      primaryContainer textPrimary
        new("graphite", "Theme.Graphite", true,  "#FF14141B", "#FF1B1B23", "#FF82B1FF", "#FF1E41AF", "#FFE4E1F0"),
        new("ocean",    "Theme.Ocean",    true,  "#FF0A1120", "#FF101C33", "#FF4FC3F7", "#FF0C5A86", "#FFDFF2FF"),
        new("forest",   "Theme.Forest",   true,  "#FF0A1510", "#FF10241A", "#FF6EE7A0", "#FF147A47", "#FFE4FBEA"),
        new("plum",     "Theme.Plum",     true,  "#FF150E26", "#FF1F1638", "#FFC79BFF", "#FF6C3BD4", "#FFF3EBFF"),
        new("sunset",   "Theme.Sunset",   true,  "#FF1B100A", "#FF241712", "#FFFF8A5C", "#FF5C2A12", "#FFF7ECE4"),
        new("midnight", "Theme.Midnight", true,  "#FF0B0F1E", "#FF111730", "#FF8B93FF", "#FF2A3266", "#FFE4E7F8"),
        new("paper",    "Theme.Paper",    false, "#FFF7F6FB", "#FFFFFFFF", "#FF3053C9", "#FFDEE1FF", "#FF1B1B22"),
        new("sand",     "Theme.Sand",     false, "#FFFBF3E4", "#FFFFFDF6", "#FFC2410C", "#FFFFE0C2", "#FF2D1B0E"),
        new("sakura",   "Theme.Sakura",   false, "#FFFDF2F6", "#FFFFFAFC", "#FFD81B60", "#FFFFDCE6", "#FF2B1220"),
        new("mint",     "Theme.Mint",     false, "#FFF1FAF5", "#FFFAFEFB", "#FF0F9D6C", "#FFD3F5E6", "#FF0C231A"),
    };

    private static string PackUriFor(string id) => id switch
    {
        "ocean" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Ocean.xaml",
        "forest" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Forest.xaml",
        "plum" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Plum.xaml",
        "sunset" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Sunset.xaml",
        "midnight" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Midnight.xaml",
        "sakura" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Sakura.xaml",
        "mint" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Mint.xaml",
        "paper" or Light => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Light.xaml",
        "sand" => "pack://application:,,,/Viora.UI;component/Themes/Tokens.Sand.xaml",
        _ => "pack://application:,,,/Viora.UI;component/Themes/Tokens.xaml",
    };

    /// <summary>
    /// Resolves "user:*" ids to a color dictionary. Assigned by the composition root
    /// (store implementation lives in Viora.App).
    /// </summary>
    public static Func<string, IReadOnlyDictionary<string, string>?>? UserPaletteResolver { get; set; }

    public static void Apply(string schemeId)
    {
        var app = Application.Current;
        if (app is null) return;

        try
        {
            ResourceDictionary palette;
            if (schemeId.StartsWith("user:", StringComparison.Ordinal))
            {
                var colors = UserPaletteResolver?.Invoke(schemeId);
                if (colors is null || colors.Count == 0) return;
                palette = BuildUserPalette(colors);
            }
            else
            {
                palette = new ResourceDictionary { Source = new Uri(PackUriFor(schemeId)) };
            }
            var merged = app.Resources.MergedDictionaries;

            int index = -1;
            for (int i = 0; i < merged.Count; i++)
            {
                if (merged[i].Contains("Color.Background")) { index = i; break; }
            }

            if (index >= 0) merged[index] = palette;
            else
            {
                // First application: insert right after Core (index of Core + 1).
                int coreIndex = -1;
                for (int i = 0; i < merged.Count; i++)
                {
                    if (merged[i].Contains("Typography.Title")) { coreIndex = i; break; }
                }
                merged.Insert(coreIndex >= 0 ? coreIndex + 1 : 0, palette);
            }

            Current = Schemes.FirstOrDefault(s => s.Id == schemeId)?.Id ?? schemeId;
            foreach (var scheme in Schemes) scheme.Refresh();
        }
        catch (Exception)
        {
            // Palette load failure keeps the current scheme; settings layer logs the change.
        }
    }

    /// <summary>
    /// Builds a full palette dictionary: starts from the dark or light base (by the
    /// background's luminance) and overrides the user-edited color families.
    /// </summary>
    private static ResourceDictionary BuildUserPalette(IReadOnlyDictionary<string, string> colors)
    {
        var background = ParseColor(Get(colors, "Background"), Color.FromRgb(0x14, 0x14, 0x1B)) ?? Color.FromRgb(0x14, 0x14, 0x1B);
        bool isDark = (background.R + background.G + background.B) / 3.0 < 128;
        var rd = new ResourceDictionary
        {
            Source = new Uri(isDark
                ? "pack://application:,,,/Viora.UI;component/Themes/Tokens.xaml"
                : "pack://application:,,,/Viora.UI;component/Themes/Tokens.Light.xaml"),
        };

        foreach (var (key, hex) in colors)
        {
            var color = ParseColor(hex, null);
            if (color is null) continue;
            rd[$"Color.{key}Color"] = color;
            rd[$"Color.{key}"] = new SolidColorBrush(color.Value);
        }

        // Derived families that must stay consistent with edits.
        if (colors.TryGetValue("Primary", out var primaryHex) && ParseColor(primaryHex, null) is { } primary)
        {
            rd["Color.Accent"] = new SolidColorBrush(primary);
            rd["Color.AccentHover"] = new SolidColorBrush(ChangeBrightness(primary, 1.12f));
            rd["Color.AccentPressed"] = new SolidColorBrush(ChangeBrightness(primary, 0.88f));
            byte a = 0x2E;
            rd["Color.StateSelected"] = new SolidColorBrush(Color.FromArgb(a, primary.R, primary.G, primary.B));
        }
        if (colors.TryGetValue("OnPrimary", out var onPrimaryHex) && ParseColor(onPrimaryHex, null) is { } onPrimary)
        {
            rd["Color.Text.OnAccent"] = new SolidColorBrush(onPrimary);
            rd["Color.AccentText"] = new SolidColorBrush(onPrimary);
        }
        if (colors.TryGetValue("TextPrimary", out var tp) && ParseColor(tp, null) is { } textPrimary)
            rd["Color.Text.Primary"] = new SolidColorBrush(textPrimary);
        if (colors.TryGetValue("TextSecondary", out var ts) && ParseColor(ts, null) is { } textSecondary)
            rd["Color.Text.Secondary"] = new SolidColorBrush(textSecondary);
        if (colors.TryGetValue("Outline", out var ol) && ParseColor(ol, null) is { } outline)
            rd["Color.Outline"] = new SolidColorBrush(outline);

        return rd;
    }

    private static string Get(IReadOnlyDictionary<string, string> colors, string key) =>
        colors.TryGetValue(key, out var v) ? v : string.Empty;

    private static Color? ParseColor(string hex, Color? fallback)
    {
        try { return (Color)ColorConverter.ConvertFromString(hex); }
        catch { return fallback; }
    }

    private static Color ChangeBrightness(Color c, float factor)
    {
        byte Clamp(float v) => (byte)Math.Clamp((int)v, 0, 255);
        return Color.FromRgb(Clamp(c.R * factor), Clamp(c.G * factor), Clamp(c.B * factor));
    }
}
