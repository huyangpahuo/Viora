using System.IO;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Viora.Core.Localization;
using Viora.Core.Plugins;
using Viora.Core.Pipeline;
using Viora.Core.Settings;
using Viora.UI.Hosting;
using Viora.UI.Localization;
using Viora.UI.Services;

namespace Viora.App;

public partial class App : Application
{
    private ServiceProvider? _serviceProvider;

    public static ServiceProvider Services =>
        ((App)Current)._serviceProvider ?? throw new InvalidOperationException("App not initialized.");

    protected override async void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var services = new ServiceCollection();
        ConfigureServices(services);

        // File logging sink, level from persisted settings once loaded (see below).
        // 日志目录支持自定义(Debug.LogFolder):settings.json 尚未经 DI 加载,这里轻量预读。
        var paths = new Viora.Infrastructure.Logging.AppPaths();
        paths.EnsureDirectories();
        string? logFolderOverride = null;
        try
        {
            var settingsFile = Path.Combine(paths.Root, "settings.json");
            if (File.Exists(settingsFile))
            {
                using var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(settingsFile));
                logFolderOverride = doc.RootElement.GetProperty("Debug").GetProperty("LogFolder").GetString();
            }
        }
        catch { /* 设置文件缺失或损坏:走默认日志目录 */ }
        var logsFolder = Viora.Core.AppLocations.ResolveLogsFolder(logFolderOverride);
        Directory.CreateDirectory(logsFolder);
        var fileLogger = new Viora.Infrastructure.Logging.FileLoggerProvider(
            Viora.Infrastructure.Logging.LogFileProvider.ComputeLogFile(logsFolder));
        services.AddLogging(logging =>
        {
            logging.SetMinimumLevel(Microsoft.Extensions.Logging.LogLevel.Debug);
            logging.AddProvider(fileLogger);
        });

        _serviceProvider = services.BuildServiceProvider();

        var logger = _serviceProvider.GetRequiredService<ILogger<App>>();
        logger.LogInformation("Viora starting (host version {Version})", HostVersion.Current);

        // WPF 绑定错误进入文件日志(含来源追踪),便于定位偶发绑定问题。
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Listeners.Add(
            new Hosting.BindingErrorTraceListener(logger));
        System.Diagnostics.PresentationTraceSources.DataBindingSource.Switch.Level =
            System.Diagnostics.SourceLevels.Error;

        // Global exception containment: log always; surface a friendly dialog in normal
        // mode, full detail only in developer mode (brief §12.2).
        DispatcherUnhandledException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unhandled UI exception");
            ShowFatal(e.Exception);
            e.Handled = true;
        };
        TaskScheduler.UnobservedTaskException += (_, e) =>
        {
            logger.LogError(e.Exception, "Unobserved task exception");
            e.SetObserved();
        };
        AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            logger.LogError((Exception)e.ExceptionObject, "Fatal unhandled exception");

        // Load the design system into app resources explicitly so failures are logged, not silent.
        try
        {
            var generic = new ResourceDictionary
            {
                Source = new Uri("pack://application:,,,/Viora.UI;component/Themes/Generic.xaml"),
            };
            foreach (var dict in generic.MergedDictionaries)
                Current.Resources.MergedDictionaries.Add(dict);
            foreach (System.Collections.DictionaryEntry kv in generic) Current.Resources[kv.Key] = kv.Value;
            logger.LogInformation("Design system loaded ({Count} keys)", Current.Resources.Count);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to load design system resources");
        }

        // Load persisted settings before any UI consumes them.
        var settings = _serviceProvider.GetRequiredService<ISettingsService>();
        await settings.LoadAsync();

        // Apply custom works folder (General.WorksFolder) before anything reads the store.
        _serviceProvider.GetRequiredService<Viora.Core.Works.IWorksStore>()
            .SetWorksFolder(Viora.Core.AppLocations.ResolveWorksFolder(settings.Current.General.WorksFolder));

        // Localization first so first paint is in the user's language.
        var localization = _serviceProvider.GetRequiredService<ILocalizationService>();
        localization.SetLanguage(settings.Current.Language.Language);
        LocalizationSource.Initialize(localization);
        Viora.UI.Theming.ThemeManager.UserPaletteResolver = id =>
        {
            var palette = id.StartsWith("user:", StringComparison.Ordinal)
                ? _serviceProvider.GetRequiredService<Viora.UI.Theming.IUserThemeStore>()
                    .Load(id["user:".Length..])
                : null;
            return palette?.Colors;
        };
        Viora.UI.Theming.ThemeManager.Apply(settings.Current.Appearance.Theme);
        ApplyFontFamily(settings.Current.General.FontFamily);

        // Live-apply theme + log level when settings change.
        settings.SettingsChanged += (_, _) =>
        {
            Viora.UI.Theming.ThemeManager.UserPaletteResolver = id =>
        {
            var palette = id.StartsWith("user:", StringComparison.Ordinal)
                ? _serviceProvider.GetRequiredService<Viora.UI.Theming.IUserThemeStore>()
                    .Load(id["user:".Length..])
                : null;
            return palette?.Colors;
        };
        Viora.UI.Theming.ThemeManager.Apply(settings.Current.Appearance.Theme);
        ApplyFontFamily(settings.Current.General.FontFamily);
            fileLogger.SetLevel(Enum.TryParse<Microsoft.Extensions.Logging.LogLevel>(
                settings.Current.Debug.LogLevel, out var lvl) ? lvl : Microsoft.Extensions.Logging.LogLevel.Information);
            fileLogger.SetRedactPaths(settings.Current.Privacy.RedactPathsInLogs);
        };

        // Bridge feature-layer exporters to the Infrastructure WIC implementations.
        Viora.Features.Convert.AnimeVector.HostExportBridge.Export =
            (buffer, stream, format, options, ct) =>
            {
                IImageExporter exporter = format switch
                {
                    Viora.Features.Convert.AnimeVector.HostExportBridge.WicFormatFromFeatures.Png => new Infrastructure.Export.PngExporter(),
                    Viora.Features.Convert.AnimeVector.HostExportBridge.WicFormatFromFeatures.Jpeg => new Infrastructure.Export.JpegExporter(),
                    _ => new Infrastructure.Export.BmpExporter(),
                };
                return exporter.ExportAsync(buffer, stream, options, ct);
            };

        var pluginHost = _serviceProvider.GetRequiredService<IPluginHost>();

        // 宿主级导出器(原先由 AnimeVector 内置特性注册;风格全部插件化后归宿主)。
        var pluginContext = ((Viora.Infrastructure.Plugins.AssemblyPluginHost)pluginHost).Context;
        pluginContext.RegisterExporter(new Viora.Features.Convert.AnimeVector.InfrastructureExportBridge.PngExporterProxy());
        pluginContext.RegisterExporter(new Viora.Features.Convert.AnimeVector.InfrastructureExportBridge.JpegExporterProxy());
        pluginContext.RegisterExporter(new Viora.Features.Convert.AnimeVector.InfrastructureExportBridge.BmpExporterProxy());

        // 客户端不预装任何插件:安装区从空开始,全部由用户在插件市场按需安装(真下载)。
        // Discover installed plugins (metadata only) and enable those not disabled.
        await pluginHost.DiscoverAsync();
        if (settings.Current.Plugins.EnablePluginLoading)
        {
            foreach (var plugin in await pluginHost.GetPluginsAsync())
            {
                if (plugin.State == PluginLoadState.Incompatible)
                    continue; // surfaced in the Plugins page with a localized error
                if (!settings.Current.Plugins.DisabledPlugins.Contains(plugin.Metadata.Id))
                    await pluginHost.EnableAsync(plugin.Metadata.Id);
            }
        }

        var mainWindow = _serviceProvider.GetRequiredService<MainWindow>();
        MainWindow = mainWindow;
        if (settings.Current.General.StartMaximized) mainWindow.WindowState = WindowState.Maximized;
        mainWindow.Show();

        logger.LogInformation("Viora started");
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _serviceProvider?.GetRequiredService<ILogger<App>>().LogInformation("Viora exiting");
        _serviceProvider?.Dispose();
        base.OnExit(e);
    }

    private static void ApplyFontFamily(string familyName)
    {
        try
        {
            Current.Resources["Font.Family"] = string.IsNullOrWhiteSpace(familyName)
                ? new System.Windows.Media.FontFamily("Inter, Segoe UI Variable Display, Segoe UI, Microsoft YaHei UI")
                : new System.Windows.Media.FontFamily(familyName);
        }
        catch
        {
            // 字体名无效:保留默认
        }
    }

    private static void ShowFatal(Exception exception)
    {
        try
        {
            bool devMode = Services.GetRequiredService<ISettingsService>().Current.Debug.DeveloperMode;
            string body = devMode
                ? exception.ToString()
                : Viora.UI.Localization.Tr.Get("Error.Generic.Body");
            MessageBox.Show(body, Viora.UI.Localization.Tr.Get("Error.Generic.Title"),
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        catch { /* never crash the crash handler */ }
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Layers below UI register themselves; App only wires the composition root.
        Viora.Infrastructure.ServiceCollectionExtensions.AddVioraInfrastructure(services);
        Viora.Localization.ServiceCollectionExtensions.AddVioraLocalization(services);
        Viora.Features.ServiceCollectionExtensions.AddVioraFeatures(services);
        Viora.UI.ServiceCollectionExtensions.AddVioraUi(services);

        // UI proxies → Infrastructure implementations (composition-root wiring).
        services.AddSingleton<IImportServiceProxy, Hosting.AppImportService>();
        services.AddSingleton<IExportProxy, Hosting.AppExportService>();
        services.AddSingleton<IUiAlert, Hosting.AppAlert>();
        services.AddSingleton<Viora.UI.Theming.IUserThemeStore, Hosting.UserThemeStore>();

        services.AddSingleton<MainWindow>();
    }
}
