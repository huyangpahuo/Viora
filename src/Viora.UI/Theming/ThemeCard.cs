using System.ComponentModel;
using System.Windows.Media;
using Viora.UI.Localization;

namespace Viora.UI.Theming;

/// <summary>A user-authored palette (persisted as JSON by IUserThemeStore).</summary>
public sealed record UserPalette(string Id, string Name, IReadOnlyDictionary<string, string> Colors);

/// <summary>Persistence for user-authored palettes (implemented in the composition root).</summary>
public interface IUserThemeStore
{
    IReadOnlyList<UserPalette> LoadAll();

    UserPalette? Load(string id);

    void Save(UserPalette palette);

    bool Delete(string id);
}

/// <summary>
/// Picker card view model: wraps either a built-in ThemeScheme or a user palette,
/// exposing uniform binding surface (swatches, name, selection, editability).
/// </summary>
public sealed class ThemeCard : INotifyPropertyChanged
{
    private readonly ThemeScheme? _builtin;

    public ThemeCard(ThemeScheme builtin)
    {
        _builtin = builtin;
        SchemeId = builtin.Id;
        IsUser = false;
        Name = string.Empty;
        _builtin.PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(ThemeScheme.IsSelected)) OnPropertyChanged(nameof(IsSelected));
        };
    }

    public ThemeCard(UserPalette palette)
    {
        Palette = palette;
        SchemeId = palette.Id;
        IsUser = true;
        Name = palette.Name;
        _ = palette.Colors.TryGetValue("Background", out var bg);
        _ = palette.Colors.TryGetValue("Surface", out var surface);
        _ = palette.Colors.TryGetValue("Primary", out var accent);
        _ = palette.Colors.TryGetValue("PrimaryContainer", out var container);
        _ = palette.Colors.TryGetValue("TextPrimary", out var text);
        PreviewBackground = bg ?? "#FF14141B";
        PreviewSurface = surface ?? "#FF1B1B23";
        PreviewAccent = accent ?? "#FF82B1FF";
        PreviewPrimaryContainer = container ?? "#FF1E41AF";
        PreviewTextPrimary = text ?? "#FFE4E1F0";
    }

    public string SchemeId { get; }

    public bool IsUser { get; }

    public UserPalette? Palette { get; }

    private ThemeScheme? Builtin => _builtin;

    public string PreviewBackground { get; } = "#FF14141B";

    public string PreviewSurface { get; } = "#FF1B1B23";

    public string PreviewAccent { get; } = "#FF82B1FF";

    public string PreviewPrimaryContainer { get; } = "#FF1E41AF";

    public string PreviewTextPrimary { get; } = "#FFE4E1F0";

    public Brush PreviewBackgroundBrush => Swatch(PreviewBackground);

    public Brush PreviewSurfaceBrush => Swatch(PreviewSurface);

    public Brush PreviewAccentBrush => Swatch(PreviewAccent);

    public Brush PreviewPrimaryContainerBrush => Swatch(PreviewPrimaryContainer);

    public Brush PreviewTextPrimaryBrush => Swatch(PreviewTextPrimary);

    public string Name { get; }

    public string LocalName => _builtin is not null ? Tr.Get(_builtin.DisplayNameKey) : Name;

    public bool IsSelected => ThemeManager.Current == SchemeId;

    public static Brush Swatch(string hex)
    {
        try { return new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex)); }
        catch { return Brushes.Transparent; }
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public void RefreshSelection() => OnPropertyChanged(nameof(IsSelected));

    private void OnPropertyChanged(string name) => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
}
