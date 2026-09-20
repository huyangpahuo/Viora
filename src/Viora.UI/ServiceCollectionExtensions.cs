using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;
using Viora.UI.Shell;
using Viora.UI.Services;
using Viora.UI.Pages.MyWorks;
using Viora.UI.Pages.Placeholder;
using Viora.UI.Pages.PluginMarket;
using Viora.UI.Pages.Settings;
using Viora.UI.Pages.Stylize;
using Viora.UI.Pages.Support;

namespace Viora.UI;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddVioraUi(this IServiceCollection services)
    {
        // Shell
        services.AddSingleton<ShellViewModel>();
        services.AddSingleton<IFeatureRegistry, FeatureRegistry>();
        services.AddSingleton<IPresetCatalog, PresetCatalog>();
        services.AddSingleton<OfficialPluginService>();
        services.AddSingleton<HotkeyService>();

        // Pages (transient; ShellViewModel caches instances)
        // StylizeViewModel 为单例:跨页“重新生成”要拿到同一个正在服务的实例。
        // SettingsViewModel 为单例:设置即时保存,页面临时重建即可拿到最新值。
        services.AddSingleton<StylizeViewModel>();
        services.AddSingleton<SettingsViewModel>();
        services.AddTransient<StylizePage>();
        services.AddTransient<MyWorksPage>();
        services.AddTransient<MyWorksViewModel>();
        services.AddTransient<PluginMarketPage>();
        services.AddTransient<PluginMarketViewModel>();
        services.AddTransient<PlaceholderPage>();
        services.AddSingleton<Pages.Support.SupportViewModel>();
        services.AddTransient<Pages.Support.AboutPage>();
        services.AddTransient<Pages.Support.HelpPage>();
        services.AddTransient<Pages.Support.FeedbackPage>();
        services.AddTransient<SettingsPage>();

        return services;
    }
}
