using System.Text.Json;

namespace Viora.Localization.Json;

/// <summary>
/// Loads JSON language packs from an embedded set (shipped with the assembly) and,
/// optionally, from an external languages folder (for user/plugin additions).
/// Pack file shape (flat key → string; keys are case-insensitive):
/// { "Nav.Home": "Home", "Settings.Title": "Settings", ... }
/// </summary>
public sealed class LanguagePack
{
    public required string Code { get; init; }

    public required string DisplayName { get; init; }

    public required Dictionary<string, string> Strings { get; init; }

    public static LanguagePack FromJson(string code, string displayName, string json)
    {
        var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Language pack '{code}' is empty or invalid.");
        return new LanguagePack { Code = code, DisplayName = displayName, Strings = strings };
    }

    internal static readonly JsonSerializerOptions JsonOptions = new()
    {
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true,
        PropertyNameCaseInsensitive = true,
    };
}
