using Microsoft.Extensions.DependencyInjection;
using Viora.UI.Hosting;
using Viora.UI.Shell;
using Viora.UI.Pages.About;
using Viora.UI.Pages.Community;
using Viora.UI.Pages.Convert;
using Viora.UI.Pages.Feedback;
using Viora.UI.Pages.Help;
using Viora.UI.Pages.Home;
using Viora.UI.Pages.Legal;
using Viora.UI.Pages.Plugins;
using Viora.UI.Pages.Settings;
using Viora.UI.Pages.Sponsor;
using Viora.UI.Services;

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
        services.AddTransient<HomePage>();
        services.AddTransient<HomeViewModel>();
        services.AddTransient<ConvertPage>();
        services.AddTransient<ConvertViewModel>();
        services.AddTransient<PluginsPage>();
        services.AddTransient<PluginsViewModel>();
        services.AddTransient<SettingsPage>();
        services.AddTransient<SettingsViewModel>();
        services.AddTransient<AboutPage>();
        services.AddTransient<AboutViewModel>();
        services.AddTransient<HelpPage>();
        services.AddTransient<HelpViewModel>();
        services.AddTransient<FeedbackPage>();
        services.AddTransient<FeedbackViewModel>();
        services.AddTransient<CommunityPage>();
        services.AddTransient<CommunityViewModel>();
        services.AddTransient<SponsorPage>();
        services.AddTransient<SponsorViewModel>();
        services.AddTransient<LegalPage>();
        services.AddTransient<LegalViewModel>();

        // Settings categories (data-driven nav inside the settings page)
        services.AddSingleton<IEnumerable<SettingCategory>>(_ => new[]
        {
            new SettingCategory("Settings.Group.General", "\uE713"),
            new SettingCategory("Settings.Group.Appearance", "\uE790"),
            new SettingCategory("Settings.Group.Language", "\uE8C1"),
            new SettingCategory("Settings.Group.ImageProcessing", "\uE8E9"),
            new SettingCategory("Settings.Group.Export", "\uEDE1"),
            new SettingCategory("Settings.Group.Plugins", "\uE116"),
            new SettingCategory("Settings.Group.Performance", "\uE9D9"),
            new SettingCategory("Settings.Group.Cache", "\uEA41"),
            new SettingCategory("Settings.Group.Privacy", "\uE72E"),
            new SettingCategory("Settings.Group.Developer", "\uE756"),
        });

        return services;
    }
}
