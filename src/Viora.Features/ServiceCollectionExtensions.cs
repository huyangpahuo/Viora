using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;

namespace Viora.Features;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraFeatures(this IServiceCollection services)
    {
        services.AddSingleton<IBuiltInFeature, Convert.AnimeVector.AnimeVectorFeature>();
        return services;
    }
}
