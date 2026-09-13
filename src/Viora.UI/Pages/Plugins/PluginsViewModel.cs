using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.UI.Hosting;
using Viora.UI.Localization;

namespace Viora.UI.Pages.Plugins;

/// <summary>One row of the "built-in presets" section on the Plugins page.</summary>
public sealed partial class BuiltInPresetItemViewModel : ObservableObject
{
    public BuiltInPresetItemViewModel(IStylePreset preset) => Model = preset;

    public IStylePreset Model { get; }

    public string Glyph => Model.IconGlyph ?? "\uE790";

    public string Name => Tr.Get(Model.DisplayNameKey);

    public string Description => Tr.Get(Model.DescriptionKey);

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

public sealed partial class PluginItemViewModel : ObservableObject
{
    public PluginItemViewModel(PluginDescriptor descriptor)
    {
        Descriptor = descriptor;
        Refresh();
    }

    public PluginDescriptor Descriptor { get; }

    public string Id => Descriptor.Metadata.Id;

    public string Name => Descriptor.Metadata.DisplayName;

    public string AuthorLine => Tr.Format("Plugins.By", Descriptor.Metadata.Author);

    public string VersionLine => Tr.Format("Plugins.Version", Descriptor.Metadata.Version.ToString());

    public string Description => Descriptor.Metadata.Description;

    public bool IsEnabled => Descriptor.State is PluginLoadState.Enabled or PluginLoadState.Loaded;

    public bool CanToggle => Descriptor.State is not (PluginLoadState.Failed or PluginLoadState.Incompatible);

    public bool HasHomepage => Descriptor.Metadata.Homepage is not null;

    public bool HasRepository => Descriptor.Metadata.Repository is not null;

    public string StateText => Descriptor.State switch
    {
        PluginLoadState.Failed => Tr.Get("Plugins.State.Failed"),
        PluginLoadState.Incompatible => Tr.Get("Plugins.State.Incompatible"),
        PluginLoadState.Disabled or PluginLoadState.Discovered => Tr.Get("Plugins.State.Disabled"),
        _ => Tr.Get("Plugins.State.Loaded"),
    };

    public bool IsFailed => Descriptor.State is PluginLoadState.Failed or PluginLoadState.Incompatible;

    partial void OnIsCheckedChanged(bool value) { }

    [ObservableProperty]
    private bool _isChecked;

    public void SetCheckedSilently(bool value) => IsChecked = value;

    public void Refresh()
    {
        IsChecked = IsEnabled;
        OnPropertyChanged(nameof(StateText));
        OnPropertyChanged(nameof(IsEnabled));
        OnPropertyChanged(nameof(IsFailed));
        OnPropertyChanged(nameof(CanToggle));
        OnPropertyChanged(nameof(AuthorLine));
        OnPropertyChanged(nameof(VersionLine));
    }

    public void RefreshLocalization()
    {
        OnPropertyChanged(nameof(AuthorLine));
        OnPropertyChanged(nameof(VersionLine));
        OnPropertyChanged(nameof(StateText));
    }
}

public partial class PluginsViewModel : Viora.UI.Pages.PageViewModel
{
    private readonly IPluginHost _host;
    private readonly IPresetCatalog _catalog;
    private readonly Shell.ShellViewModel _shell;
    private readonly ILogger<PluginsViewModel> _logger;
    private readonly IUiAlert _alert;

    public PluginsViewModel(
        IPluginHost host,
        IPresetCatalog catalog,
        Shell.ShellViewModel shell,
        ILogger<PluginsViewModel> logger,
        IUiAlert alert)
    {
        _host = host;
        _catalog = catalog;
        _shell = shell;
        _logger = logger;
        _alert = alert;
        _host.PluginStateChanged += async (_, _) => await ReloadAsync();
        _catalog.Changed += (_, _) => LoadBuiltInPresets();
        LoadBuiltInPresets();
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var p in BuiltInPresets) p.Refresh();
        };
    }

    public override string TitleKey => "Plugins.Title";

    public override string? SubtitleKey => "Plugins.Subtitle";

    public ObservableCollection<BuiltInPresetItemViewModel> BuiltInPresets { get; } = new();

    public ObservableCollection<PluginItemViewModel> Plugins { get; } = new();

    public bool HasNoPlugins => Plugins.Count == 0;

    /// <summary>
    /// 系统预设:按约定,内置功能注册的预设 Id 以 "builtin." 开头
    /// (见 docs/plugin-development.md)。始终可用,无需安装。
    /// </summary>
    private void LoadBuiltInPresets()
    {
        var builtIn = _catalog.Presets
            .Where(p => p.Id.StartsWith("builtin.", StringComparison.Ordinal))
            .OrderBy(p => p.Id, StringComparer.Ordinal)
            .ToList();

        // Changed fires on every registration; skip no-op rebuilds.
        if (builtIn.Count == BuiltInPresets.Count &&
            builtIn.Select(p => p.Id).SequenceEqual(BuiltInPresets.Select(p => p.Model.Id)))
            return;

        BuiltInPresets.Clear();
        foreach (var preset in builtIn)
            BuiltInPresets.Add(new BuiltInPresetItemViewModel(preset));
    }

    [RelayCommand]
    private async Task LoadedAsync() => await ReloadAsync();

    [RelayCommand]
    private void UsePreset(BuiltInPresetItemViewModel? vm)
    {
        if (vm is null) return;
        var convert = _shell.Items.FirstOrDefault(i => i.PageType == typeof(Convert.ConvertPage));
        if (convert is not null) _shell.SelectedItem = convert;
    }

    public async Task ReloadAsync()
    {
        var descriptors = await _host.GetPluginsAsync();
        Plugins.Clear();
        foreach (var d in descriptors)
        {
            var vm = new PluginItemViewModel(d);
            vm.PropertyChanged += async (_, e) =>
            {
                if (e.PropertyName == nameof(PluginItemViewModel.IsChecked))
                    await ToggleAsync(vm);
            };
            Plugins.Add(vm);
        }
        OnPropertyChanged(nameof(HasNoPlugins));
    }

    private async Task ToggleAsync(PluginItemViewModel vm)
    {
        try
        {
            if (vm.IsChecked)
                await _host.EnableAsync(vm.Id);
            else
                await _host.DisableAsync(vm.Id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Plugin toggle failed for {PluginId}", vm.Id);
            vm.SetCheckedSilently(!vm.IsChecked);
            _alert.Warn(Tr.Get("Error.Generic.Title"), Tr.Get("Error.Generic.Body"));
        }
    }

    [RelayCommand]
    private async Task InstallAsync()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "Viora plugin|*.vplugin;*.zip" };
        if (dialog.ShowDialog() != true) return;

        var result = await _host.InstallFromPackageAsync(dialog.FileName);
        if (result.Success)
        {
            await _host.DiscoverAsync();
            var id = result.Detail!;
            await _host.LoadAsync(id);
            await _host.EnableAsync(id);
            _alert.Info(Tr.Get("Plugins.Install.Succeeded").Replace("{0}", id));
        }
        else
        {
            _alert.Warn(Tr.Get("Plugins.Install.Failed"),
                result.ErrorCode == "invalid_package" ? Tr.Get("Plugins.Install.InvalidPackage")
                : result.ErrorCode == "duplicate" ? Tr.Get("Plugins.Install.Duplicate")
                : result.Detail ?? string.Empty);
        }
    }

    [RelayCommand]
    private async Task UninstallAsync(PluginItemViewModel? vm)
    {
        if (vm is null) return;
        if (!_alert.Confirm(
                Tr.Get("Plugins.Uninstall.Confirm.Title"),
                Tr.Format("Plugins.Uninstall.Confirm.Body", vm.Name)))
            return;

        await _host.UninstallAsync(vm.Id);
        await ReloadAsync();
    }

    [RelayCommand]
    private void OpenHomepage(PluginItemViewModel? vm)
    {
        if (vm?.Descriptor.Metadata.Homepage is { } url)
            Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
    }

    [RelayCommand]
    private void OpenRepository(PluginItemViewModel? vm)
    {
        if (vm?.Descriptor.Metadata.Repository is { } url)
            Process.Start(new ProcessStartInfo(url.ToString()) { UseShellExecute = true });
    }

    [RelayCommand]
    private async Task ReloadPluginsAsync() => await ReloadAsync();
}

/// <summary>UI alert facade (dialogs implemented in App to keep UI project testable).</summary>
public interface IUiAlert
{
    void Info(string message);

    void Warn(string title, string message);

    bool Confirm(string title, string message);
}
