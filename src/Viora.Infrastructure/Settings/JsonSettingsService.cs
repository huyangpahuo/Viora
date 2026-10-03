using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Viora.Core.Settings;
using Viora.Infrastructure.Logging;

namespace Viora.Infrastructure.Settings;

/// <summary>
/// JSON settings persistence: single versioned document at %LOCALAPPDATA%\Viora\settings.json.
/// Atomic writes (temp + replace). Update() applies a mutation and triggers debounced save +
/// SettingsChanged so UI and behaviors react immediately.
/// </summary>
public sealed partial class JsonSettingsService : ISettingsService
{
    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly IAppPaths _paths;
    private readonly SemaphoreSlim _ioLock = new(1, 1);
    private readonly System.Timers.Timer _debounce;

    private VioraSettings _current = new();

    public JsonSettingsService(IAppPaths paths)
    {
        _paths = paths;
        _debounce = new System.Timers.Timer(500) { AutoReset = false };
        _debounce.Elapsed += async (_, _) => await SaveAsync();
    }

    public VioraSettings Current => _current;

    public event EventHandler? SettingsChanged;

    public async Task<VioraSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            _current = await ReadCore(cancellationToken) ?? new VioraSettings();
        }
        finally
        {
            _ioLock.Release();
        }
        return _current;
    }

    public async Task SaveAsync(CancellationToken cancellationToken = default)
    {
        await _ioLock.WaitAsync(cancellationToken);
        try
        {
            _paths.EnsureDirectories();
            var temp = _paths.SettingsFile + ".tmp";
            await using (var stream = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await JsonSerializer.SerializeAsync(stream, _current, SerializerOptions, cancellationToken);
            }

            if (File.Exists(_paths.SettingsFile))
                File.Replace(temp, _paths.SettingsFile, destinationBackupFileName: null);
            else
                File.Move(temp, _paths.SettingsFile);
        }
        finally
        {
            _ioLock.Release();
        }
    }

    public void Update(Action<VioraSettings> mutate)
    {
        mutate(_current);
        SettingsChanged?.Invoke(this, EventArgs.Empty);
        _debounce.Stop();
        _debounce.Start();
    }

    private async Task<VioraSettings?> ReadCore(CancellationToken cancellationToken)
    {
        if (!File.Exists(_paths.SettingsFile)) return null;
        try
        {
            await using var stream = new FileStream(_paths.SettingsFile, FileMode.Open, FileAccess.Read, FileShare.Read);
            var loaded = await JsonSerializer.DeserializeAsync<VioraSettings>(stream, SerializerOptions, cancellationToken);
            if (loaded is null) return null;
            if (loaded.SchemaVersion > new VioraSettings().SchemaVersion)
            {
                // 未来版本的设置:备份后按默认值继续。绝不阻断启动,也绝不覆盖用户文件。
                TryBackupSettingsFile(_paths.SettingsFile + ".newer");
                return null;
            }
            return loaded; // missing properties fill from defaults automatically
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            // 损坏/被占用:备份后按默认值启动 —— 任何用户数据问题都不允许阻断启动。
            TryBackupSettingsFile(_paths.SettingsFile + ".corrupt");
            return null;
        }
    }

    private void TryBackupSettingsFile(string backupPath)
    {
        try { File.Copy(_paths.SettingsFile, backupPath, overwrite: true); } catch { }
    }
}
