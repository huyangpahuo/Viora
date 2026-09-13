using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Viora.Core.Imaging;
using Viora.Core.Pipeline;
using Viora.Core.Plugins;
using Viora.Core.Settings;
using Viora.UI.Hosting;
using Viora.UI.Localization;
using Viora.UI.Services;

namespace Viora.UI.Pages.Convert;

public sealed partial class PresetItemViewModel : ObservableObject
{
    public PresetItemViewModel(IStylePreset preset)
    {
        Model = preset;
        Name = Tr.Get(preset.DisplayNameKey);
        Description = Tr.Get(preset.DescriptionKey);
    }

    public IStylePreset Model { get; }

    public string Name { get; }

    public string Description { get; }

    public void Refresh()
    {
        OnPropertyChanged(nameof(Name));
        OnPropertyChanged(nameof(Description));
    }
}

public sealed partial class ParameterItemViewModel : ObservableObject
{
    private readonly IPresetParameter _model;

    public ParameterItemViewModel(IPresetParameter model)
    {
        _model = model;
        Label = Tr.Get(model.DisplayNameKey);
        Description = Tr.Get(model.DisplayNameKey + ".Description");
        switch (model.Kind)
        {
            case "slider":
                double value = System.Convert.ToDouble(model.DefaultValue);
                Minimum = System.Convert.ToDouble(model.MinValue);
                Maximum = System.Convert.ToDouble(model.MaxValue);
                Step = System.Convert.ToDouble(model.Step);
                SliderValue = value;
                break;
            default:
                SliderValue = System.Convert.ToDouble(model.DefaultValue);
                Minimum = System.Convert.ToDouble(model.MinValue);
                Maximum = System.Convert.ToDouble(model.MaxValue);
                Step = 1;
                break;
        }
    }

    public IPresetParameter Model => _model;

    public string Key => _model.Key;

    public string Label { get; }

    public string Description { get; }

    public string Kind => _model.Kind;

    [ObservableProperty]
    private double _sliderValue;

    public double Minimum { get; }

    public double Maximum { get; }

    public double Step { get; }

    public string ValueDisplay =>
        Step >= 1 ? Math.Round(SliderValue).ToString() : SliderValue.ToString("0.00");

    partial void OnSliderValueChanged(double value) => OnPropertyChanged(nameof(ValueDisplay));

    public void Refresh() { OnPropertyChanged(nameof(Label)); OnPropertyChanged(nameof(Description)); }

    public object ToParameterValue() =>
        Step >= 1 ? (object)System.Convert.ToInt32(Math.Round(SliderValue)) : SliderValue;

    public void Reset() => SliderValue = System.Convert.ToDouble(_model.DefaultValue);
}

public partial class ConvertViewModel : PageViewModel
{
    private readonly IPresetCatalog _catalog;
    private readonly IImportServiceProxy _import;
    private readonly IImageConversionEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IExportProxy _export;
    private readonly ILogger<ConvertViewModel> _logger;

    private IImageBuffer? _sourceBuffer;
    private IImageBuffer? _resultBuffer;
    private CancellationTokenSource? _cts;

    public ConvertViewModel(
        IPresetCatalog catalog,
        IImportServiceProxy import,
        IImageConversionEngine engine,
        ISettingsService settings,
        IExportProxy export,
        ILogger<ConvertViewModel> logger)
    {
        _catalog = catalog;
        _import = import;
        _engine = engine;
        _settings = settings;
        _export = export;
        _logger = logger;

        Presets = new ObservableCollection<PresetItemViewModel>();
        Parameters = new ObservableCollection<ParameterItemViewModel>();
        LoadPresets();
        _catalog.Changed += (_, _) => LoadPresets();
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var p in Presets) p.Refresh();
            foreach (var p in Parameters) p.Refresh();
        };
    }

    public override string TitleKey => "Convert.Title";

    public override string? SubtitleKey => "Convert.Subtitle";

    public ObservableCollection<PresetItemViewModel> Presets { get; }

    public ObservableCollection<ParameterItemViewModel> Parameters { get; }

    [ObservableProperty]
    private PresetItemViewModel? _selectedPreset;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _originalImageSource;

    [ObservableProperty]
    private System.Windows.Media.ImageSource? _resultImageSource;

    [ObservableProperty]
    private bool _hasImage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _statusText = Tr.Get("Convert.Idle");

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _splitPosition = 0.5;

    [ObservableProperty]
    private CompareMode _compareMode = CompareMode.Split;

    public bool ShowCompareSplit => CompareMode == CompareMode.Split && HasImage && ResultImageSource is not null;

    public bool ShowCompareSide => CompareMode == CompareMode.SideBySide && HasImage && ResultImageSource is not null;

    public bool ShowOriginalOnly => HasImage && ResultImageSource is null;

    partial void OnCompareModeChanged(CompareMode value)
    {
        OnPropertyChanged(nameof(ShowCompareSplit));
        OnPropertyChanged(nameof(ShowCompareSide));
    }

    partial void OnResultImageSourceChanged(System.Windows.Media.ImageSource? value)
    {
        OnPropertyChanged(nameof(ShowCompareSplit));
        OnPropertyChanged(nameof(ShowCompareSide));
        OnPropertyChanged(nameof(ShowOriginalOnly));
    }

    partial void OnSelectedPresetChanged(PresetItemViewModel? value)
    {
        Parameters.Clear();
        if (value is null) return;
        foreach (var p in value.Model.Parameters)
        {
            var vm = new ParameterItemViewModel(p);
            vm.PropertyChanged += async (_, e) => { if (e.PropertyName == nameof(vm.SliderValue)) await AutoRerunAsync(); };
            Parameters.Add(vm);
        }
    }

    partial void OnZoomChanged(double value) => OnPropertyChanged(nameof(ZoomPercent));

    public string ZoomPercent => $"{Math.Round(Zoom * 100)}%";

    public string ConvertButtonText => IsBusy ? Tr.Get("Convert.Cancel") : Tr.Get("Convert.Convert");

    partial void OnIsBusyChanged(bool value) => OnPropertyChanged(nameof(ConvertButtonText));

    private void LoadPresets()
    {
        var selected = SelectedPreset?.Model.Id;
        Presets.Clear();
        foreach (var preset in _catalog.Presets)
            Presets.Add(new PresetItemViewModel(preset));
        SelectedPreset = Presets.FirstOrDefault(p => p.Model.Id == selected) ?? Presets.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ImportAsync(string? path)
    {
        if (IsBusy) return;

        try
        {
            if (string.IsNullOrEmpty(path))
            {
                path = _import.PickFile();
                if (string.IsNullOrEmpty(path)) return;
            }

            int cap = _settings.Current.Performance.UsePreviewQualityDuringInteraction
                ? _settings.Current.ImageProcessing.PreviewMaxDimension
                : _settings.Current.ImageProcessing.ExportMaxDimension;

            IsBusy = true;
            StatusText = Tr.Get("Common.Loading");
            _sourceBuffer = await _import.LoadFromFileAsync(path, cap);
            ResultBuffer = null;
            OriginalImageSource = _import.ToImageSource(_sourceBuffer);
            HasImage = true;
            Zoom = 1.0;
            StatusText = Tr.Get("Convert.Idle");
            await RunConversionAsync(preview: true);
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == Services.ImageImportProxyErrorCode.Unsupported)
        {
            ShowError("Convert.Import.Unsupported", ex);
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == ImageImportProxyErrorCode.TooLarge)
        {
            ShowError("Convert.Import.TooLarge", ex);
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == ImageImportProxyErrorCode.Corrupt)
        {
            ShowError("Convert.Import.Corrupt", ex);
        }
        catch (Exception ex)
        {
            ShowError("Convert.Import.Failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task ConvertAsync()
    {
        if (IsBusy) { _cts?.Cancel(); return; }
        await RunConversionAsync(preview: false);
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    [RelayCommand]
    private void ResetParameters()
    {
        foreach (var p in Parameters) p.Reset();
    }

    [RelayCommand]
    private async Task ExportAsync()
    {
        if (ResultBuffer is null) return;

        try
        {
            var path = _export.PickSavePath(_settings.Current.Export.DefaultFormat);
            if (string.IsNullOrEmpty(path)) return;

            IsBusy = true;
            StatusText = Tr.Get("Common.Loading");

            // Re-run at export quality if the source is a preview-res result.
            var buffer = ResultBuffer;
            if (_sourceBuffer is { } src && (buffer.Width < src.Width || buffer.Height < src.Height))
            {
                StatusText = Tr.Get("Convert.Progress").Replace("{0}", "…");
                buffer = await RunPipelineAsync(_sourceBuffer, preview: false, CancellationToken.None);
            }

            await _export.ExportAsync(buffer, path, _settings.Current.Export.JpegQuality);
            StatusText = Tr.Format("Convert.Export.SavedTo", Path.GetFileName(path));
            _logger.LogInformation("Exported conversion result to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            StatusText = Tr.Get("Convert.Export.Failed");
            if (_settings.Current.Debug.DeveloperMode)
                MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task AutoRerunAsync()
    {
        if (IsBusy || !HasImage || SelectedPreset is null) return;
        if (!_settings.Current.Performance.UsePreviewQualityDuringInteraction) return;
        await RunConversionAsync(preview: true);
    }

    private async Task RunConversionAsync(bool preview)
    {
        if (_sourceBuffer is null || SelectedPreset is null) return;

        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            IsBusy = true;
            var progress = new Progress<PipelineProgress>(p =>
            {
                ProgressFraction = p.OverallFraction;
                StatusText = string.Format(Tr.Get("Convert.Progress"), (int)(p.OverallFraction * 100));
            });

            var parameters = new Dictionary<string, object>();
            foreach (var p in Parameters) parameters[p.Key] = p.ToParameterValue();

            var buffer = await RunPipelineAsync(_sourceBuffer, preview, ct, progress);
            ResultBuffer = buffer;
            ResultImageSource = _import.ToImageSource(buffer);
            StatusText = Tr.Get("Convert.Idle");
        }
        catch (OperationCanceledException)
        {
            StatusText = Tr.Get("Convert.Conversion.Cancelled");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Conversion failed");
            StatusText = Tr.Get("Convert.Conversion.Failed");
            if (_settings.Current.Debug.DeveloperMode)
                MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    private async Task<IImageBuffer> RunPipelineAsync(
        IImageBuffer source, bool preview, CancellationToken ct, IProgress<PipelineProgress>? progress = null)
    {
        var parameters = new Dictionary<string, object>();
        foreach (var p in Parameters) parameters[p.Key] = p.ToParameterValue();

        var pipeline = SelectedPreset!.Model.BuildPipeline(parameters);
        var result = await _engine.ExecuteAsync(pipeline, source, parameters, preview, progress, ct);
        return result.Result;
    }

    private IImageBuffer? ResultBuffer
    {
        get => _resultBuffer;
        set { _resultBuffer = value; OnPropertyChanged(nameof(ResultImageSource)); }
    }

    private void ShowError(string key, Exception ex)
    {
        _logger.LogError(ex, "{Key}", key);
        StatusText = Tr.Get(key);
        if (_settings.Current.Debug.DeveloperMode)
            MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
    }

    partial void OnHasImageChanged(bool value) => OnPropertyChanged(nameof(ShowOriginalOnly));
}

public enum CompareMode
{
    Split,
    SideBySide,
}
