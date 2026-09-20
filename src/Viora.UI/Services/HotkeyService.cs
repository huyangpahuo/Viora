using Viora.Core.Settings;

namespace Viora.UI.Services;

/// <summary>一条可配置快捷键的定义(id 持久化键 + 本地化标签 + 默认组合键)。</summary>
public sealed record HotkeyDefinition(string Id, string LabelKey, string DefaultGesture);

/// <summary>
/// 快捷键注册表:定义 → 读取 → 修改(持久化到 Hotkeys.Bindings,空串 = 未设置)。
/// 新功能要加快捷键时,只需在 Defaults 里加一条定义并在使用处调用 GetGesture —— 设置页自动出现可录制行。
/// </summary>
public sealed class HotkeyService
{
    private readonly ISettingsService _settings;

    public HotkeyService(ISettingsService settings) => _settings = settings;

    public static readonly HotkeyDefinition[] Defaults =
    [
        new("stylize.run", "Hotkey.Stylize.Run", "Ctrl+Enter"),
        new("stylize.open", "Hotkey.Stylize.Open", "Ctrl+O"),
        new("nav.stylize", "Hotkey.Nav.Stylize", "Ctrl+1"),
        new("nav.works", "Hotkey.Nav.Works", "Ctrl+2"),
        new("nav.market", "Hotkey.Nav.Market", "Ctrl+3"),
        new("nav.settings", "Hotkey.Nav.Settings", "Ctrl+4"),
    ];

    /// <summary>当前生效的组合键串(未自定义时回退默认)。</summary>
    public string GetGesture(string id)
    {
        var custom = _settings.Current.Hotkeys.Bindings;
        var def = Defaults.First(d => d.Id == id);
        return custom.TryGetValue(id, out var g) && !string.IsNullOrWhiteSpace(g) ? g : def.DefaultGesture;
    }

    /// <summary>设置组合键(空串 = 清除)。返回冲突的其它定义(无冲突返回 null),不落地冲突修改。</summary>
    public HotkeyDefinition? SetGesture(string id, string? gesture)
    {
        var normalized = string.IsNullOrWhiteSpace(gesture) ? string.Empty : gesture.Trim();
        if (normalized.Length > 0)
        {
            foreach (var other in Defaults)
            {
                if (other.Id == id) continue;
                if (string.Equals(GetGesture(other.Id), normalized, StringComparison.OrdinalIgnoreCase))
                    return other;
            }
        }

        _settings.Update(s => s.Hotkeys.Bindings[id] = normalized);
        return null;
    }

    /// <summary>组合键串 → KeyGesture(失败返回 null)。</summary>
    public static System.Windows.Input.KeyGesture? ToKeyGesture(string gesture)
    {
        try
        {
            return new System.Windows.Input.KeyGestureConverter().ConvertFromInvariantString(gesture)
                as System.Windows.Input.KeyGesture;
        }
        catch
        {
            return null;
        }
    }
}
