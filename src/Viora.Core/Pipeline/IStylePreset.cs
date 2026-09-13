namespace Viora.Core.Pipeline;

/// <summary>Descriptor of a single tunable parameter exposed by a preset.</summary>
public interface IPresetParameter
{
    string Key { get; }

    string DisplayNameKey { get; }

    object DefaultValue { get; }

    object MinValue { get; }

    object MaxValue { get; }

    object Step { get; }

    /// <summary>UI hint: "slider", "toggle", "option".</summary>
    string Kind { get; }

    /// <summary>For "option" kind: value → localization key.</summary>
    IReadOnlyDictionary<string, string>? Options { get; }
}

public sealed record PresetParameter(
    string Key,
    string DisplayNameKey,
    object DefaultValue,
    object MinValue,
    object MaxValue,
    object Step,
    string Kind = "slider",
    IReadOnlyDictionary<string, string>? Options = null) : IPresetParameter;

/// <summary>
/// A stylization preset: a named, parameterized pipeline composition.
/// Implemented by built-in features (Viora.Features) and by plugins alike.
/// </summary>
public interface IStylePreset
{
    string Id { get; }

    /// <summary>Localization key, not raw text.</summary>
    string DisplayNameKey { get; }

    /// <summary>Localization key for a one-line description.</summary>
    string DescriptionKey { get; }

    string? IconGlyph { get; }

    IReadOnlyList<IPresetParameter> Parameters { get; }

    IReadOnlyList<IImageProcessingStage> BuildPipeline(IReadOnlyDictionary<string, object> parameters);
}
