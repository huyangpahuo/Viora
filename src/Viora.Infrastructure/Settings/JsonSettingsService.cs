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
                throw new InvalidDataException("Settings were written by a newer Viora.");
            return loaded; // missing properties fill from defaults automatically
        }
        catch (JsonException ex)
        {
            // Corrupt settings: back them up, start fresh — never block startup.
            try { File.Copy(_paths.SettingsFile, _paths.SettingsFile + ".corrupt", overwrite: true); } catch { }
            throw new InvalidDataException("Settings file was corrupt.", ex);
        }
    }
}
