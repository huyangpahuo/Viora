namespace Viora.Core.Localization;

/// <summary>
/// Localization contract. Implementations hold language packs; the UI binds through
/// a notification-enabled adapter so language switches apply live.
/// </summary>
public interface ILocalizationService
{
    string CurrentLanguage { get; }

    IReadOnlyList<string> AvailableLanguages { get; }

    event EventHandler? LanguageChanged;

    string GetString(string key);

    void SetLanguage(string languageCode);

    /// <summary>Registers additional strings (e.g. from a plugin) scoped under a prefix.</summary>
    void RegisterStrings(IReadOnlyDictionary<string, string> stringsByLanguageAndKey);
}
