namespace Viora.Core.Plugins;

/// <summary>Shared contract surface for plugin-vs-host version compatibility checks.</summary>
public static class HostVersion
{
    /// <summary>Running host version, from the app assembly.</summary>
    public static Version Current { get; } =
        System.Reflection.Assembly.GetEntryAssembly()?.GetName().Version ?? new Version(1, 0, 0);
}

/// <summary>Minimal semantic-version range: "1.2.3", ">=1.2.3", ">=1.0.0 <2.0.0".</summary>
public sealed record VersionRange
{
    private const string AnyToken = "*";

    public static VersionRange Any { get; } = new() { Raw = AnyToken };

    public string Raw { get; private init; } = AnyToken;

    private readonly List<(Version Version, bool Inclusive, bool IsMax)> _bounds = new();

    public static VersionRange Parse(string raw)
    {
        raw = raw?.Trim() ?? AnyToken;
        if (raw is "" or AnyToken or "any") return Any;

        var range = new VersionRange { Raw = raw };
        foreach (var token in raw.Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            bool isMax = token.StartsWith('<');
            bool inclusive = token.StartsWith(">=") || token.StartsWith("<=");
            var digits = token.TrimStart('>', '<', '=');
            var v = Version.TryParse(digits.Count(c => c == '.') == 2 ? digits : digits + ".0", out var parsed)
                ? parsed
                : throw new FormatException($"Invalid version '{token}' in range '{raw}'.");
            range._bounds.Add((v, inclusive, isMax));
        }

        return range;
    }

    public bool Contains(Version version)
    {
        if (_bounds.Count == 0) return true;
        foreach (var (bound, inclusive, isMax) in _bounds)
        {
            int cmp = version.CompareTo(bound);
            if (isMax ? (inclusive ? cmp > 0 : cmp >= 0) : (inclusive ? cmp < 0 : cmp <= 0))
                return false;
        }

        return true;
    }
}

public sealed record PluginDependency(string PluginId, VersionRange VersionRange);

public sealed record PluginMetadata(
    string Id,
    string DisplayName,
    Version Version,
    string Author,
    string Description,
    Uri? Homepage,
    Uri? Repository,
    VersionRange RequiredHostVersion,
    IReadOnlyList<string> Capabilities,
    IReadOnlyList<PluginDependency> Dependencies);

public enum PluginLoadState
{
    Discovered,
    Loaded,
    Enabled,
    Disabled,
    Failed,
    Incompatible,
}

public sealed record PluginDescriptor(
    PluginMetadata Metadata,
    PluginLoadState State,
    string? ErrorMessage,
    bool IsBuiltin);
