using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viora.Core.Localization;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Settings;
using Viora.Localization.Json;

namespace Viora.Localization;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraLocalization(this IServiceCollection services)
    {
        services.AddSingleton<JsonLocalizationService>();
        services.AddSingleton<ILocalizationService>(sp => sp.GetRequiredService<JsonLocalizationService>());
        return services;
    }
}
