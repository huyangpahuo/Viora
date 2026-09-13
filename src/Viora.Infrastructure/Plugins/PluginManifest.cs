using System.IO;
using System.Text.Json;

namespace Viora.Infrastructure.Plugins;

/// <summary>Deserialized shape of plugin.json (metadata-only, used at discovery and load).</summary>
public sealed class PluginManifest
{
    public string Id { get; set; } = string.Empty;

    public string DisplayName { get; set; } = string.Empty;

    public string Version { get; set; } = "1.0.0";

    public string Author { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    public string? Homepage { get; set; }

    public string? Repository { get; set; }

    public string RequiredHostVersion { get; set; } = "*";

    public string EntryAssembly { get; set; } = string.Empty;

    public string TypeName { get; set; } = string.Empty;

    public List<string> Capabilities { get; set; } = new();

    public List<PluginDependencyDto> Dependencies { get; set; } = new();

    public static PluginManifest? TryLoad(string path)
    {
        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PluginManifest>(json, JsonOpts);
        }
        catch
        {
            return null;
        }
    }

    public void Save(string path) =>
        File.WriteAllText(path, JsonSerializer.Serialize(this, JsonOpts));

    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        PropertyNameCaseInsensitive = true,
    };
}

public sealed class PluginDependencyDto
{
    public string PluginId { get; set; } = string.Empty;

    public string VersionRange { get; set; } = "*";
}
