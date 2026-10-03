using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Viora.Core.Localization;

namespace Viora.Localization.Json;

public sealed partial class JsonLocalizationService : ILocalizationService
{
    public const string FallbackLanguage = "en";

    private readonly ILogger<JsonLocalizationService> _logger;
    private readonly Dictionary<string, LanguagePack> _packs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<(string Scope, string Key), string> _pluginStrings = new();
    private readonly object _gate = new();

    private string _current = FallbackLanguage;

    public JsonLocalizationService(ILogger<JsonLocalizationService> logger)
    {
        _logger = logger;
        LoadExternalPacks();
        LogPacksLoaded(_logger, _packs.Count, string.Join(",", _packs.Keys));
    }

    /// <summary>
    /// 语言包全部外置(exe 旁 languages\*.json,含内置的 zh-Hans / English,与第三方语言包
    /// 同一机制):按文件名识别语言码(如 fr.json → fr),`Language.DisplayName` 键决定下拉显示名。
    /// </summary>
    private void LoadExternalPacks()
    {
        try
        {
            var dir = Path.Combine(AppContext.BaseDirectory, "languages");
            if (!Directory.Exists(dir)) return;

            foreach (var file in Directory.EnumerateFiles(dir, "*.json"))
            {
                try
                {
                    var code = Path.GetFileNameWithoutExtension(file);
                    // 语言码合法性:2-3 字母(可带 -脚本/地区后缀),避免误扫杂物
                    if (!Regex.IsMatch(code, @"^[a-z]{2,3}(-[A-Za-z]+)*$", RegexOptions.IgnoreCase)) continue;

                    var strings = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(file));
                    if (strings is null || strings.Count == 0) continue;
                    // 空值 = 未翻译占位:剔除后走内置回退链,避免界面显示空白
                    foreach (var key in strings.Where(kv => string.IsNullOrWhiteSpace(kv.Value)).Select(kv => kv.Key).ToList())
                        strings.Remove(key);
                    if (strings.Count == 0) continue;

                    var displayName = strings.TryGetValue("Language.DisplayName", out var dn) && !string.IsNullOrEmpty(dn)
                        ? dn
                        : code;

                    lock (_gate)
                    {
                        if (_packs.TryGetValue(code, out var existing))
                        {
                            // 已有内置/先到的包:逐键覆盖,但跳过空值——
                            // 半成品翻译(空串占位)应回退内置文案而不是把界面变成空白
                            foreach (var (key, value) in strings)
                            {
                                if (string.IsNullOrWhiteSpace(value)) continue;
                                existing.Strings[key] = value;
                            }
                        }
                        else
                        {
                            _packs[code] = new LanguagePack { Code = code, DisplayName = displayName, Strings = strings };
                        }
                    }
                    LogExternalPackLoaded(_logger, code, strings.Count);
                }
                catch (Exception ex)
                {
                    LogExternalPackFailed(_logger, file, ex);
                }
            }
        }
        catch (Exception ex)
        {
            LogExternalPackFailed(_logger, "languages", ex);
        }
    }

    [LoggerMessage(EventId = 10, Level = LogLevel.Information,
        Message = "External language pack loaded: {Code} ({Count} keys)")]
    private static partial void LogExternalPackLoaded(ILogger logger, string code, int count);

    [LoggerMessage(EventId = 11, Level = LogLevel.Warning,
        Message = "Failed to load external language pack {File}")]
    private static partial void LogExternalPackFailed(ILogger logger, string file, Exception ex);

    [LoggerMessage(EventId = 9, Level = LogLevel.Information,
        Message = "Loaded {Count} language packs: {Codes}")]
    private static partial void LogPacksLoaded(ILogger logger, int count, string codes);

    public string CurrentLanguage => _current;

    public IReadOnlyList<string> AvailableLanguages
    {
        get
        {
            lock (_gate)
                return _packs.Values.OrderBy(p => p.DisplayName).Select(p => p.Code).ToList();
        }
    }

    public event EventHandler? LanguageChanged;

    public string GetString(string key)
    {
        if (string.IsNullOrEmpty(key)) return string.Empty;

        lock (_gate)
        {
            if (_pluginStrings.TryGetValue((_current, key), out var scoped)
                || _pluginStrings.TryGetValue((FallbackLanguage, key), out scoped))
                return scoped;

            if (_packs.TryGetValue(_current, out var pack) && pack.Strings.TryGetValue(key, out var value))
                return value;

            if (_packs.TryGetValue(FallbackLanguage, out var fallback)
                && fallback.Strings.TryGetValue(key, out var fbValue))
            {
                LogMissingKeyFallback(_logger, key, _current);
                return fbValue;
            }
        }

        LogKeyMissingEverywhere(_logger, key);
        return DevModeMarker ? $"[{key}]" : FallbackText;
    }

    public void SetLanguage(string languageCode)
    {
        bool changed;
        lock (_gate)
        {
            if (!_packs.ContainsKey(languageCode))
            {
                LogUnknownLanguage(_logger, languageCode);
                return;
            }

            changed = _current != languageCode;
            _current = languageCode;
        }

        if (changed) LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RegisterStrings(IReadOnlyDictionary<string, string> stringsByLanguageAndKey)
    {
        lock (_gate)
        {
            foreach (var (scopedKey, value) in stringsByLanguageAndKey)
                _pluginStrings[(ExtractLanguage(scopedKey), NormalizeKey(scopedKey))] = value;
        }

        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    private static string ExtractLanguage(string scopedKey)
    {
        int idx = scopedKey.IndexOf("::", StringComparison.Ordinal);
        return idx > 0 ? scopedKey[..idx] : FallbackLanguage;
    }

    private static string NormalizeKey(string scopedKey)
    {
        int idx = scopedKey.IndexOf("::", StringComparison.Ordinal);
        return idx > 0 ? scopedKey[(idx + 2)..] : scopedKey;
    }

    internal void RegisterPack(LanguagePack pack)
    {
        lock (_gate)
            _packs[pack.Code] = pack;
        LanguageChanged?.Invoke(this, EventArgs.Empty);
    }

    internal static bool DevModeMarker { get; set; }

    private const string FallbackText = "…";

    [LoggerMessage(EventId = 1, Level = LogLevel.Debug,
        Message = "Localization key {Key} missing for language {Language}; used English fallback")]
    private static partial void LogMissingKeyFallback(ILogger logger, string key, string language);

    [LoggerMessage(EventId = 2, Level = LogLevel.Warning,
        Message = "Localization key {Key} missing in every language pack")]
    private static partial void LogKeyMissingEverywhere(ILogger logger, string key);

    [LoggerMessage(EventId = 3, Level = LogLevel.Warning,
        Message = "Requested language {Language} has no loaded pack; keeping current language")]
    private static partial void LogUnknownLanguage(ILogger logger, string language);
}
