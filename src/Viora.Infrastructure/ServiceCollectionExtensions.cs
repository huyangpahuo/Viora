using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Diagnostics;
using Viora.Core.Settings;
using Viora.Infrastructure.Export;
using Viora.Infrastructure.Imaging;
using Viora.Infrastructure.Logging;
using Viora.Infrastructure.Pipeline;
using Viora.Infrastructure.Plugins;
using Viora.Infrastructure.Settings;
using Viora.Infrastructure.Works;
using Viora.Core.Works;

namespace Viora.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraInfrastructure(this IServiceCollection services)
    {
        services.AddSingleton<ISettingsService, JsonSettingsService>();
        services.AddSingleton<IAppPaths, AppPaths>();
        services.AddSingleton<ILogFileProvider, LogFileProvider>();
                services.AddSingleton<ImageBufferAdapter>();
        services.AddSingleton<IImageConversionEngine, ImageConversionEngine>();
        services.AddSingleton<AssemblyPluginHost>();
        services.AddSingleton<IPluginHost>(sp => sp.GetRequiredService<AssemblyPluginHost>());
        services.AddSingleton<IPluginContext>(sp => sp.GetRequiredService<AssemblyPluginHost>().Context);
        services.AddSingleton<IImportService, ImportService>();
        services.AddSingleton<IWorksStore, JsonWorksStore>();
        return services;
    }
}
