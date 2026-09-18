using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;
using Viora.UI.Shell;
using Viora.UI.Pages.MyWorks;
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
        // StylizeViewModel 为单例:跨页“重新生成”要拿到同一个正在服务的实例。
        services.AddSingleton<StylizeViewModel>();
        services.AddTransient<StylizePage>();
        services.AddTransient<MyWorksPage>();
        services.AddTransient<MyWorksViewModel>();
        services.AddTransient<PlaceholderPage>();

        return services;
    }
}
