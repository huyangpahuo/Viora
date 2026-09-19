using Viora.Core.Pipeline;
using Viora.Core.Plugins;

namespace Viora.UI.Hosting;

/// <summary>
/// Aggregated catalog of conversion presets and exporters from built-in features
/// and loaded plugins, plus the plugin registry surface used by the Plugins page.
/// UI binds to this; it never talks to individual features directly.
/// </summary>
public interface IPresetCatalog
{
    event EventHandler? Changed;

    IReadOnlyList<IStylePreset> Presets { get; }

    IReadOnlyList<IImageExporter> Exporters { get; }

    void Add(IStylePreset preset);

    void Add(IImageExporter exporter);

    /// <summary>按预设 id 移除(插件卸载时由市场调用),返回是否确有移除。</summary>
    bool RemoveById(string presetId);
}

public sealed class PresetCatalog : IPresetCatalog
{
    private readonly object _gate = new();
    private readonly List<IStylePreset> _presets = new();
    private readonly List<IImageExporter> _exporters = new();

    public event EventHandler? Changed;

    public IReadOnlyList<IStylePreset> Presets
    {
        get { lock (_gate) return _presets.ToList(); }
    }

    public IReadOnlyList<IImageExporter> Exporters
    {
        get { lock (_gate) return _exporters.ToList(); }
    }

    public void Add(IStylePreset preset)
    {
        lock (_gate) _presets.Add(preset);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public void Add(IImageExporter exporter)
    {
        lock (_gate) _exporters.Add(exporter);
        Changed?.Invoke(this, EventArgs.Empty);
    }

    public bool RemoveById(string presetId)
    {
        lock (_gate)
        {
            var existing = _presets.FirstOrDefault(p => p.Id == presetId);
            if (existing is null) return false;
            _presets.Remove(existing);
        }
        Changed?.Invoke(this, EventArgs.Empty);
        return true;
    }
}
