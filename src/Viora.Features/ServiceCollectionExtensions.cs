using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;

namespace Viora.Features;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IBuiltInFeature, Convert.AnimeVector.AnimeVectorFeature>();

        // Built-in style presets (each wraps a single IStylePreset through the plugin seam).
        services.AddSingleton<IBuiltInFeature>(_ => new Convert.Styles.StyleFeature(
            new Convert.Styles.MosaicPreset(), "builtin.viora.mosaic", "Mosaic",
            "Ceramic tile mosaic stylization."));
        services.AddSingleton<IBuiltInFeature>(_ => new Convert.Styles.StyleFeature(
            new Convert.Styles.OilPaintingPreset(), "builtin.viora.oil-painting", "Van Gogh Oil",
            "Impasto oil painting stylization."));
        services.AddSingleton<IBuiltInFeature>(_ => new Convert.Styles.StyleFeature(
            new Convert.Styles.SketchPreset(), "builtin.viora.sketch", "Pencil Sketch",
            "Minimal line-drawing stylization."));
        services.AddSingleton<IBuiltInFeature>(_ => new Convert.Styles.StyleFeature(
            new Convert.Styles.WatercolorPreset(), "builtin.viora.watercolor", "Watercolor",
            "Wet watercolor wash stylization."));

        // The 30-style registry (print / photo / handcraft / misc groups).
        foreach (var (preset, featureId, displayName, description) in Convert.Styles.BuiltInStyles.All)
        {
            var captured = (preset, featureId, displayName, description);
            services.AddSingleton<IBuiltInFeature>(_ => new Convert.Styles.StyleFeature(
                captured.preset, captured.featureId, captured.displayName, captured.description));
        }

        return services;
    }
}
