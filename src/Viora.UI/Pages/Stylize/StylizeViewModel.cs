using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
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

namespace Viora.UI.Pages.Stylize;

/// <summary>One parameter row of the selected style (slider rendering + pipeline value).</summary>
public sealed partial class ParameterItemViewModel : ObservableObject
{
    private readonly IPresetParameter _model;

    public ParameterItemViewModel(IPresetParameter model)
    {
        _model = model;
        Minimum = System.Convert.ToDouble(model.MinValue);
        Maximum = System.Convert.ToDouble(model.MaxValue);
        Step = model.Kind == "slider" ? System.Convert.ToDouble(model.Step) : 1;
        SliderValue = System.Convert.ToDouble(model.DefaultValue);
    }

    public IPresetParameter Model => _model;

    public string Key => _model.Key;

    public string Label => Tr.Get(_model.DisplayNameKey);

    public string Description => Tr.Get(_model.DisplayNameKey + ".Description");

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

/// <summary>
/// One style card in the right panel. Category is derived from the preset id (cosmetic
/// filtering); the tile brush is a deterministic brand-adjacent gradient so every style
/// keeps a stable identity without bundled preview assets.
/// </summary>
public sealed partial class StyleItemViewModel : ObservableObject
{
    public StyleItemViewModel(IStylePreset preset)
    {
        Model = preset;
        (CategoryKey, TileBrush) = Resolve(preset.Id);
    }

    public IStylePreset Model { get; }

    public string Name => Tr.Get(Model.DisplayNameKey);

    public string Description => Tr.Get(Model.DescriptionKey);

    public string CategoryKey { get; }

    public Brush TileBrush { get; }

    public void Refresh() => OnPropertyChanged(nameof(Name));

    private static readonly string[] HotIds =
    {
        "builtin.anime-vector", "builtin.oil-painting", "builtin.watercolor",
        "builtin.mosaic", "builtin.sketch", "builtin.neon-cyberpunk",
    };

    public bool IsHot => HotIds.Contains(Model.Id);

    private static (string category, Brush tile) Resolve(string id)
    {
        string category = id switch
        {
            var s when HotIds.Contains(s) => "Style.Category.Hot",
            var s when Contains(s, "anime", "comic", "manga", "cartoon") => "Style.Category.Anime",
            var s when Contains(s, "oil", "watercolor", "sketch", "fresco", "etching", "engraving",
                "hatching", "stippling", "woodcut", "glass", "marble", "smoke", "sand", "string",
                "tape", "paper", "embroidery", "crayon", "chalkboard", "blueprint", "risograph",
                "halftone", "dithered", "collage") => "Style.Category.Art",
            var s when Contains(s, "exposure", "film", "infrared", "polaroid", "thermal", "xray", "crt") => "Style.Category.Realistic",
            _ => "Style.Category.Other",
        };

        // Deterministic violet→blue→cyan family gradient per style id.
        uint hash = 2166136261;
        foreach (char c in id) { hash ^= c; hash *= 16777619; }
        double t = (hash % 1000) / 1000.0;
        var left = Color.FromRgb(0x8F, (byte)(0x7B + t * 0x10), (byte)(0xFF - t * 0x30));
        var right = Color.FromRgb((byte)(0x5B + t * 0x10), (byte)(0x8C + t * 0x20), 0xE8);
        var brush = new LinearGradientBrush
        {
            StartPoint = new Point(0, 0),
            EndPoint = new Point(1, 1),
            GradientStops =
            {
                new GradientStop(left, 0),
                new GradientStop(right, 1),
            },
        };
        brush.Freeze();
        return (category, brush);
    }

    private static bool Contains(string s, params string[] keys) => keys.Any(s.Contains);
}

/// <summary>One imported image (工作项): source + latest result, drives one thumbnail.</summary>
public sealed partial class WorkItemViewModel : ObservableObject
{
    public WorkItemViewModel(string fileName, IImageBuffer sourceBuffer, ImageSource sourceImage)
    {
        FileName = fileName;
        SourceBuffer = sourceBuffer;
        SourceImage = sourceImage;
    }

    public string FileName { get; }

    public IImageBuffer SourceBuffer { get; }

    public ImageSource SourceImage { get; }

    public string Title => Path.GetFileNameWithoutExtension(FileName);

    [ObservableProperty]
    private ImageSource? _resultImage;

    [ObservableProperty]
    private bool _isFailed;

    public IImageBuffer? ResultBuffer { get; set; }

    public bool HasResult => ResultImage is not null;

    partial void OnResultImageChanged(ImageSource? value) => OnPropertyChanged(nameof(HasResult));
}

public partial class StylizeViewModel : ObservableObject
{
    private readonly IPresetCatalog _catalog;
    private readonly IImportServiceProxy _import;
    private readonly IImageConversionEngine _engine;
    private readonly ISettingsService _settings;
    private readonly IExportProxy _export;
    private readonly ILogger<StylizeViewModel> _logger;

    private CancellationTokenSource? _cts;
    private int _runId;

    public StylizeViewModel(
        IPresetCatalog catalog,
        IImportServiceProxy import,
        IImageConversionEngine engine,
        ISettingsService settings,
        IExportProxy export,
        ILogger<StylizeViewModel> logger)
    {
        _catalog = catalog;
        _import = import;
        _engine = engine;
        _settings = settings;
        _export = export;
        _logger = logger;

        Styles = new ObservableCollection<StyleItemViewModel>();
        Parameters = new ObservableCollection<ParameterItemViewModel>();
        Items = new ObservableCollection<WorkItemViewModel>();
        LoadStyles();
        _catalog.Changed += (_, _) => LoadStyles();
        LocalizationSource.Current.PropertyChanged += (_, _) =>
        {
            foreach (var s in Styles) s.Refresh();
            foreach (var p in Parameters) p.Refresh();
            OnPropertyChanged(nameof(CtaText));
            OnPropertyChanged(nameof(ItemsSummary));
            if (!IsBusy) StatusText = Tr.Get("Stylize.Idle");
        };
    }

    // ---------- 风格 ----------

    public ObservableCollection<StyleItemViewModel> Styles { get; }

    public ObservableCollection<ParameterItemViewModel> Parameters { get; }

    [ObservableProperty]
    private StyleItemViewModel? _selectedStyle;

    [ObservableProperty]
    private string _categoryFilter = "Style.Category.All";

    public string[] CategoryFilters { get; } =
    {
        "Style.Category.All", "Style.Category.Hot", "Style.Category.Anime",
        "Style.Category.Art", "Style.Category.Realistic", "Style.Category.Other",
    };

    public bool HasStyles => Styles.Count > 0;

    /// <summary>Filtered view over Styles (category chips + search kept simple).</summary>
    public IEnumerable<StyleItemViewModel> FilteredStyles =>
        CategoryFilter == "Style.Category.All" ? Styles : Styles.Where(s => s.CategoryKey == CategoryFilter);

    partial void OnCategoryFilterChanged(string value) => OnPropertyChanged(nameof(FilteredStyles));

    partial void OnSelectedStyleChanged(StyleItemViewModel? value)
    {
        Parameters.Clear();
        if (value is null) return;

        foreach (var p in value.Model.Parameters)
        {
            var vm = new ParameterItemViewModel(p);
            vm.PropertyChanged += async (_, e) => { if (e.PropertyName == nameof(vm.SliderValue)) await AutoRerunAsync(); };
            Parameters.Add(vm);
        }

        RunCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void ResetParameters()
    {
        foreach (var p in Parameters) p.Reset();
        _ = AutoRerunAsync();
    }

    // ---------- 工作项(缩略图) ----------

    public ObservableCollection<WorkItemViewModel> Items { get; }

    [ObservableProperty]
    private WorkItemViewModel? _selectedItem;

    public string ItemsSummary => Items.Count == 0 ? "0 / 0" : $"{(SelectedIndex + 1)} / {Items.Count}";

    private int SelectedIndex => SelectedItem is null ? -1 : Items.IndexOf(SelectedItem);

    partial void OnSelectedItemChanged(WorkItemViewModel? value)
    {
        if (_observedItem is not null) _observedItem.PropertyChanged -= OnItemPropertyChanged;
        _observedItem = value;
        if (value is not null) value.PropertyChanged += OnItemPropertyChanged;

        OnPropertyChanged(nameof(ItemsSummary));
        OnPropertyChanged(nameof(HasImage));
        OnPropertyChanged(nameof(OriginalImageSource));
        OnPropertyChanged(nameof(ResultImageSource));
        OnPropertyChanged(nameof(CanCompare));
        RunCommand.NotifyCanExecuteChanged();
        RefreshViewFlags();
    }

    [RelayCommand]
    private void NextItem()
    {
        if (Items.Count == 0) return;
        SelectedItem = Items[(SelectedIndex + 1) % Items.Count];
    }

    [RelayCommand]
    private void PreviousItem()
    {
        if (Items.Count == 0) return;
        SelectedItem = Items[(SelectedIndex - 1 + Items.Count) % Items.Count];
    }

    [RelayCommand]
    private void DeleteCurrentItem()
    {
        if (SelectedItem is null) return;
        int index = SelectedIndex;
        Items.Remove(SelectedItem);
        SelectedItem = Items.Count == 0 ? null : Items[Math.Min(index, Items.Count - 1)];
    }

    // ---------- 视图状态 ----------

    public bool HasImage => SelectedItem is not null;

    public ImageSource? OriginalImageSource => SelectedItem?.SourceImage;

    public ImageSource? ResultImageSource => SelectedItem?.ResultImage;

    [ObservableProperty]
    private bool _isBusy;

    [ObservableProperty]
    private double _progressFraction;

    [ObservableProperty]
    private string _statusText = Tr.Get("Stylize.Idle");

    [ObservableProperty]
    private double _zoom = 1.0;

    [ObservableProperty]
    private double _panX;

    [ObservableProperty]
    private double _panY;

    [ObservableProperty]
    private CompareMode _compareMode = CompareMode.Split;

    /// <summary>Before/After 分割线位置(0..1)。</summary>
    [ObservableProperty]
    private double _splitPosition = 0.5;

    [ObservableProperty]
    private bool _aiQualityMode;

    /// <summary>勾选后以完整质量跑一次(预览通道关闭)。</summary>
    public bool UseFullQuality => AiQualityMode || !_settings.Current.Performance.UsePreviewQualityDuringInteraction;

    public bool HasResult => ResultImageSource is not null;

    public bool CanCompare => HasImage && HasResult;

    public bool ShowOriginalOnly => HasImage && (CompareMode == CompareMode.Original || !CanCompare);

    public bool ShowCompareSplit => CanCompare && CompareMode == CompareMode.Split;

    public bool ShowCompareSide => CanCompare && CompareMode == CompareMode.SideBySide;

    public bool ShowResultOnly => CanCompare && CompareMode == CompareMode.Result;

    public bool Zoomed => Zoom > 1.001;

    public string ZoomPercent => $"{Math.Round(Zoom * 100)}%";

    public string CtaText => IsBusy ? Tr.Get("Stylize.Cancel") : Tr.Get("Stylize.Start");

    partial void OnIsBusyChanged(bool value)
    {
        OnPropertyChanged(nameof(CtaText));
        RunCommand.NotifyCanExecuteChanged();
    }

    partial void OnCompareModeChanged(CompareMode value) => RefreshViewFlags();

    private WorkItemViewModel? _observedItem;

    private void OnItemPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName is nameof(WorkItemViewModel.ResultImage) or nameof(WorkItemViewModel.IsFailed))
        {
            OnPropertyChanged(nameof(ResultImageSource));
            RefreshViewFlags();
        }
    }

    private void RefreshViewFlags()
    {
        OnPropertyChanged(nameof(HasResult));
        OnPropertyChanged(nameof(CanCompare));
        OnPropertyChanged(nameof(ShowOriginalOnly));
        OnPropertyChanged(nameof(ShowCompareSplit));
        OnPropertyChanged(nameof(ShowCompareSide));
        OnPropertyChanged(nameof(ShowResultOnly));
    }

    partial void OnZoomChanged(double value)
    {
        OnPropertyChanged(nameof(ZoomPercent));
        OnPropertyChanged(nameof(Zoomed));
        if (value <= 1.001) { PanX = 0; PanY = 0; }
    }

    [RelayCommand]
    private void ZoomIn() => Zoom = Math.Min(6.0, Zoom * 1.3);

    [RelayCommand]
    private void ZoomOut() => Zoom = Math.Max(1.0, Zoom / 1.3);

    [RelayCommand]
    private void ZoomFit() => Zoom = 1.0;

    /// <summary>双击分割区 → 分割线回中。</summary>
    [RelayCommand]
    private void ResetSplit() => SplitPosition = 0.5;

    // ---------- 工作模式 Tab ----------

    [ObservableProperty]
    private WorkMode _mode = WorkMode.Image;

    public bool IsImageMode => Mode == WorkMode.Image;

    public bool IsBatchMode => Mode == WorkMode.Batch;

    public bool IsHistoryMode => Mode == WorkMode.History;

    partial void OnModeChanged(WorkMode value)
    {
        OnPropertyChanged(nameof(IsImageMode));
        OnPropertyChanged(nameof(IsBatchMode));
        OnPropertyChanged(nameof(IsHistoryMode));
    }

    // ---------- 导入 ----------

    [RelayCommand]
    private async Task ImportAsync()
    {
        var path = _import.PickFile();
        if (!string.IsNullOrEmpty(path)) await ImportFilesAsync(new[] { path });
    }

    /// <summary>拖拽入口:一次可加入多张,全部加入后跳到第一张新图。</summary>
    [RelayCommand]
    private async Task ImportFilesAsync(string[]? paths)
    {
        if (paths is null || paths.Length == 0) return;
        try
        {
            IsBusy = true;
            StatusText = Tr.Get("Common.Loading");

            int cap = _settings.Current.Performance.UsePreviewQualityDuringInteraction
                ? _settings.Current.ImageProcessing.PreviewMaxDimension
                : _settings.Current.ImageProcessing.ExportMaxDimension;

            WorkItemViewModel? firstNew = null;
            foreach (var path in paths)
            {
                var buffer = await _import.LoadFromFileAsync(path, cap);
                var item = new WorkItemViewModel(path, buffer, _import.ToImageSource(buffer));
                Items.Add(item);
                firstNew ??= item;
            }

            if (firstNew is not null)
            {
                SelectedItem = firstNew;
                Zoom = 1.0;
                StatusText = Tr.Get("Stylize.Idle");
                await AutoRerunAsync();
            }
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == Services.ImageImportProxyErrorCode.Unsupported)
        {
            ShowError("Stylize.Import.Unsupported", ex);
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == ImageImportProxyErrorCode.TooLarge)
        {
            ShowError("Stylize.Import.TooLarge", ex);
        }
        catch (ImageImportProxyException ex) when (ex.ErrorCode == ImageImportProxyErrorCode.Corrupt)
        {
            ShowError("Stylize.Import.Corrupt", ex);
        }
        catch (Exception ex)
        {
            ShowError("Stylize.Import.Failed", ex);
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>示例图片:内置资源解包到临时文件后走正常导入管线。</summary>
    [RelayCommand]
    private async Task LoadSampleAsync()
    {
        try
        {
            var uri = new Uri("pack://application:,,,/Viora.UI;component/sample.jpg");
            var info = Application.GetResourceStream(uri) ?? throw new FileNotFoundException("sample.jpg resource missing");
            string tempPath = Path.Combine(Path.GetTempPath(), "viora-sample.jpg");
            using (var stream = info.Stream)
            using (var file = File.Create(tempPath))
                await stream.CopyToAsync(file);

            await ImportFilesAsync(new[] { tempPath });
        }
        catch (Exception ex)
        {
            ShowError("Stylize.Import.Failed", ex);
        }
    }

    // ---------- 风格化执行 ----------

    private bool CanRun() => HasImage && SelectedStyle is not null;

    /// <summary>开始风格化(Ctrl + Enter):对当前工作项执行一次完整风格化。</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private async Task RunAsync()
    {
        var item = SelectedItem;
        var style = SelectedStyle;
        if (item is null || style is null) return;

        int id = ++_runId;
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var ct = _cts.Token;

        try
        {
            IsBusy = true;
            item.IsFailed = false;
            var progress = new Progress<PipelineProgress>(p =>
            {
                if (id != _runId) return;
                ProgressFraction = p.OverallFraction;
                StatusText = string.Format(Tr.Get("Stylize.Progress"), (int)(p.OverallFraction * 100));
            });

            var parameters = new Dictionary<string, object>();
            foreach (var p in Parameters) parameters[p.Key] = p.ToParameterValue();

            var pipeline = style.Model.BuildPipeline(parameters);
            var result = await _engine.ExecuteAsync(pipeline, item.SourceBuffer, parameters, !UseFullQuality, progress, ct);
            if (id != _runId) return;

            item.ResultBuffer = result.Result;
            item.ResultImage = _import.ToImageSource(result.Result);
            StatusText = Tr.Get("Stylize.Idle");
            if (CompareMode == CompareMode.Original) CompareMode = CompareMode.Split;
        }
        catch (OperationCanceledException)
        {
            if (id == _runId) StatusText = Tr.Get("Stylize.Idle");
        }
        catch (Exception ex)
        {
            if (id != _runId) return;
            _logger.LogError(ex, "Stylization failed");
            StatusText = Tr.Get("Stylize.Failed");
            item.IsFailed = true;
            if (_settings.Current.Debug.DeveloperMode)
                MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            if (id == _runId) IsBusy = false;
        }
    }

    /// <summary>失败状态的 重新生成。</summary>
    [RelayCommand]
    private Task RetryAsync() => RunAsync();

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private async Task AutoRerunAsync()
    {
        if (!HasImage || SelectedStyle is null) return;
        await RunAsync();
    }

    // ---------- 导出 ----------

    [RelayCommand]
    private async Task ExportAsync()
    {
        var item = SelectedItem;
        if (item?.ResultBuffer is null) return;

        try
        {
            var path = _export.PickSavePath(_settings.Current.Export.DefaultFormat);
            if (string.IsNullOrEmpty(path)) return;

            IsBusy = true;
            StatusText = Tr.Get("Common.Loading");

            // 预览质量的结果在导出时重跑一次完整质量。
            var buffer = item.ResultBuffer;
            if (buffer.Width < item.SourceBuffer.Width || buffer.Height < item.SourceBuffer.Height)
            {
                StatusText = Tr.Get("Stylize.Progress").Replace("{0}", "…");
                var parameters = new Dictionary<string, object>();
                foreach (var p in Parameters) parameters[p.Key] = p.ToParameterValue();
                var pipeline = SelectedStyle!.Model.BuildPipeline(parameters);
                var result = await _engine.ExecuteAsync(pipeline, item.SourceBuffer, parameters, false, null, CancellationToken.None);
                buffer = result.Result;
            }

            await _export.ExportAsync(buffer, path, _settings.Current.Export.JpegQuality);
            StatusText = Tr.Format("Stylize.Export.SavedTo", Path.GetFileName(path));
            _logger.LogInformation("Exported stylized image to {Path}", path);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed");
            StatusText = Tr.Get("Stylize.Export.Failed");
            if (_settings.Current.Debug.DeveloperMode)
                MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
        }
    }

    // ---------- 内部 ----------

    private void LoadStyles()
    {
        var selected = SelectedStyle?.Model.Id;
        Styles.Clear();
        foreach (var preset in _catalog.Presets)
            Styles.Add(new StyleItemViewModel(preset));
        OnPropertyChanged(nameof(HasStyles));
        OnPropertyChanged(nameof(FilteredStyles));

        // Selection survives a catalog refresh (param changes reset Parameters).
        SelectedStyle = Styles.FirstOrDefault(s => s.Model.Id == selected) ?? Styles.FirstOrDefault();
    }

    private void ShowError(string key, Exception ex)
    {
        _logger.LogError(ex, "{Key}", key);
        StatusText = Tr.Get(key);
        if (_settings.Current.Debug.DeveloperMode)
            MessageBox.Show(ex.ToString(), "Viora", MessageBoxButton.OK, MessageBoxImage.Error);
    }
}

/// <summary>中央工作区底部 Tab:图片 / 批量处理 / 历史记录。</summary>
public enum WorkMode
{
    Image,
    Batch,
    History,
}

/// <summary>对比视图模式,分屏为默认。</summary>
public enum CompareMode
{
    Original,
    Split,
    SideBySide,
    Result,
}
