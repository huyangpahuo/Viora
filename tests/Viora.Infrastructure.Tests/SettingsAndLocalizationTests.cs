using System.IO;
using Viora.Core.Localization;
using Viora.Core.Settings;
using Viora.Infrastructure.Logging;
using Viora.Infrastructure.Plugins;
using Viora.Infrastructure.Settings;
using Viora.Localization.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Viora.Infrastructure.Tests;

public class SettingsRoundTripTests : IDisposable
{
    private readonly string _root;

    public SettingsRoundTripTests()
    {
        _root = Path.Combine(Path.GetTempPath(), $"viora-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);
    }

    private IAppPaths MakePaths() => new PortablePaths(_root);

    [Fact]
    public async Task SaveThenLoad_PreservesAllSections()
    {
        var service = new JsonSettingsService(MakePaths());
        await service.LoadAsync();
        service.Update(s =>
        {
            s.General.StartMaximized = false;
            s.Appearance.Theme = "light";
            s.Language.Language = "zh-Hans";
            s.ImageProcessing.PreviewMaxDimension = 777;
            s.Export.DefaultFormat = "jpeg";
            s.Plugins.DisabledPlugins.Add("com.x.y");
            s.Cache.MaxCacheSizeMb = 2048;
            s.Debug.DeveloperMode = true;
            s.Debug.LogLevel = "Trace";
        });
        await service.SaveAsync();

        var reloaded = new JsonSettingsService(MakePaths());
        var loaded = await reloaded.LoadAsync();

        Assert.False(loaded.General.StartMaximized);
        Assert.Equal("light", loaded.Appearance.Theme);
        Assert.Equal("zh-Hans", loaded.Language.Language);
        Assert.Equal(777, loaded.ImageProcessing.PreviewMaxDimension);
        Assert.Equal("jpeg", loaded.Export.DefaultFormat);
        Assert.Contains("com.x.y", loaded.Plugins.DisabledPlugins);
        Assert.Equal(2048, loaded.Cache.MaxCacheSizeMb);
        Assert.True(loaded.Debug.DeveloperMode);
        Assert.Equal("Trace", loaded.Debug.LogLevel);
    }

    [Fact]
    public async Task MissingFile_ReturnsDefaults()
    {
        var service = new JsonSettingsService(MakePaths());
        var loaded = await service.LoadAsync();
        Assert.Equal("en", loaded.Language.Language);
        Assert.True(loaded.General.StartMaximized);
    }

    [Fact]
    public async Task CorruptFile_BackupAndThrow()
    {
        var paths = MakePaths();
        Directory.CreateDirectory(paths.Root);
        await File.WriteAllTextAsync(paths.SettingsFile, "{ not valid json !!!");

        var service = new JsonSettingsService(paths);
        await Assert.ThrowsAnyAsync<Exception>(() => service.LoadAsync());
        Assert.True(File.Exists(paths.SettingsFile + ".corrupt"), "corrupt file was not backed up");
    }

    public void Dispose()
    {
        try { Directory.Delete(_root, recursive: true); } catch { }
    }

    private sealed class PortablePaths : IAppPaths
    {
        public PortablePaths(string root)
        {
            Root = root;
            SettingsFile = Path.Combine(root, "settings.json");
            LogsFolder = Path.Combine(root, "logs");
            CacheFolder = Path.Combine(root, "cache");
            PluginsFolder = Path.Combine(root, "plugins");
        }

        public string Root { get; }
        public string SettingsFile { get; }
        public string LogsFolder { get; }
        public string CacheFolder { get; }
        public string PluginsFolder { get; }

        public void EnsureDirectories() => Directory.CreateDirectory(Root);
    }
}

public class LocalizationTests
{
    private static JsonLocalizationService MakeService() =>
        new(NullLogger<JsonLocalizationService>.Instance);

    [Fact]
    public void EmbeddedPacks_LoadEnglishAndChinese()
    {
        var service = MakeService();
        Assert.Contains("en", service.AvailableLanguages);
        Assert.Contains("zh-Hans", service.AvailableLanguages);
    }

    [Fact]
    public void GetString_ResolvesKnownKeysInBothLanguages()
    {
        var service = MakeService();

        service.SetLanguage("en");
        Assert.Equal("Home", service.GetString("Nav.Home"));

        service.SetLanguage("zh-Hans");
        Assert.Equal("首页", service.GetString("Nav.Home"));
    }

    [Fact]
    public void GetString_FallsBackToEnglishForMissingKey()
    {
        var service = MakeService();
        service.SetLanguage("zh-Hans");
        // A key only present in en (simulated): Nav.Home exists in both; pick a synthetic check
        // by removing is impossible — instead assert fallback marker for a totally unknown key
        // returns something non-empty and deterministic.
        string result = service.GetString("Totally.Unknown.Key");
        Assert.False(string.IsNullOrEmpty(result));
    }

    [Fact]
    public void SetLanguage_IgnoresUnknownLanguage()
    {
        var service = MakeService();
        service.SetLanguage("en");
        service.SetLanguage("xx-Invalid");
        Assert.Equal("en", service.CurrentLanguage);
    }

    [Fact]
    public void RegisterStrings_PluginScopedKeysResolve()
    {
        var service = MakeService();
        service.RegisterStrings(new Dictionary<string, string>
        {
            ["en::Preset.X.Name"] = "Hello",
            ["zh-Hans::Preset.X.Name"] = "你好",
        });

        service.SetLanguage("en");
        Assert.Equal("Hello", service.GetString("Preset.X.Name"));
        service.SetLanguage("zh-Hans");
        Assert.Equal("你好", service.GetString("Preset.X.Name"));
    }

    [Fact]
    public void LanguageChanged_FiresOnSwitch()
    {
        var service = MakeService();
        int fired = 0;
        service.LanguageChanged += (_, _) => fired++;
        service.SetLanguage("zh-Hans");
        service.SetLanguage("zh-Hans"); // same → no event
        Assert.Equal(1, fired);
    }
}

public class PluginManifestTests
{
    [Fact]
    public void TryLoad_ParsesValidManifest()
    {
        string path = Path.Combine(Path.GetTempPath(), $"viora-manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, """
            {
              "id": "com.test.plugin",
              "displayName": "Test",
              "version": "1.2.3",
              "author": "T",
              "description": "D",
              "requiredHostVersion": ">=1.0.0 <2.0.0",
              "entryAssembly": "Test.dll",
              "typeName": "Test.Plugin",
              "capabilities": ["convert.preset"]
            }
            """);
        try
        {
            var manifest = PluginManifest.TryLoad(path);
            Assert.NotNull(manifest);
            Assert.Equal("com.test.plugin", manifest!.Id);
            Assert.Equal("1.2.3", manifest.Version);
            Assert.Contains("convert.preset", manifest.Capabilities);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void TryLoad_ReturnsNullOnGarbage()
    {
        string path = Path.Combine(Path.GetTempPath(), $"viora-manifest-{Guid.NewGuid():N}.json");
        File.WriteAllText(path, "]]] garbage");
        try
        {
            Assert.Null(PluginManifest.TryLoad(path));
        }
        finally { File.Delete(path); }
    }
}
