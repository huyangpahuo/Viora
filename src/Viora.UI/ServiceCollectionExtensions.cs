using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;
using Viora.UI.Shell;
using Viora.UI.Pages.Placeholder;
using Viora.UI.Pages.Stylize;

namespace Viora.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraUi(this IServiceCollection services)
    {
        // Shell
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IFeatureRegistry, FeatureRegistry>();
        services.AddSingleton<IPresetCatalog, PresetCatalog>();

        // Pages (transient; ShellViewModel caches instances)
        services.AddTransient<StylizePage>();
        services.AddTransient<StylizeViewModel>();
        services.AddTransient<PlaceholderPage>();

        return services;
    }
}
