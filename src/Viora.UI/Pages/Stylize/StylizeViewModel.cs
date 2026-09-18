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
using Viora.Core.Works;
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

    /// <summary>批量处理队列中的状态(单图模式忽略)。</summary>
    [ObservableProperty]
    private BatchItemState _batchState = BatchItemState.Pending;

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
    private readonly IWorksStore _works;
    private readonly ILogger<StylizeViewModel> _logger;

    /// <summary>从“我的作品”重新生成时携带的原标题/来源名,落一条新作品后清空。</summary>
    private string? _pendingTitle;
    private string? _pendingSourceFileName;

    private CancellationTokenSource? _cts;
    private CancellationTokenSource? _batchCts;
    private int _runId;

    public StylizeViewModel(
        IPresetCatalog catalog,
        IImportServiceProxy import,
        IImageConversionEngine engine,
        ISettingsService settings,
        IExportProxy export,
        IWorksStore works,
        ILogger<StylizeViewModel> logger)
    {
        _catalog = catalog;
        _import = import;
        _engine = engine;
        _settings = settings;
        _export = export;
        _works = works;
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

    /// <summary>当前风格。拒绝 null 赋值:风格卡片分页翻页时 ListBox 会清空选择,
    /// 不能因此丢掉正在使用的风格与参数。</summary>
    public StyleItemViewModel? SelectedStyle
    {
        get => _selectedStyle;
        set
        {
            if (value is null || ReferenceEquals(_selectedStyle, value)) return;
            if (SetProperty(ref _selectedStyle, value)) OnSelectedStyleChanged(value);
        }
    }

    private StyleItemViewModel? _selectedStyle;

    [ObservableProperty]
    private string _categoryFilter = "Style.Category.All";

    public string[] CategoryFilters { get; } =
    {
        "Style.Category.All", "Style.Category.Hot", "Style.Category.Anime",
        "Style.Category.Art", "Style.Category.Realistic", "Style.Category.Other",
    };

    public bool HasStyles => Styles.Count > 0;

    /// <summary>风格卡片每页数量(3 列 × 4 行)。</summary>
    public const int StylesPageSize = 12;

    [ObservableProperty]
    private int _stylePage = 1;

    /// <summary>Filtered view over Styles (category chips + search kept simple).</summary>
    public IEnumerable<StyleItemViewModel> FilteredStyles =>
        CategoryFilter == "Style.Category.All" ? Styles : Styles.Where(s => s.CategoryKey == CategoryFilter);

    public int StyleTotalItems => FilteredStyles.Count();

    public int StyleTotalPages => Math.Max(1, (int)Math.Ceiling(StyleTotalItems / (double)StylesPageSize));

    public bool StylePagerVisible => StyleTotalPages > 1;

    public IEnumerable<StyleItemViewModel> PagedStyles =>
        FilteredStyles.Skip((Math.Clamp(StylePage, 1, StyleTotalPages) - 1) * StylesPageSize).Take(StylesPageSize);

    private void RefreshStylePaging()
    {
        OnPropertyChanged(nameof(StyleTotalItems));
        OnPropertyChanged(nameof(StyleTotalPages));
        OnPropertyChanged(nameof(StylePagerVisible));
        OnPropertyChanged(nameof(PagedStyles));
    }

    partial void OnStylePageChanged(int value) => OnPropertyChanged(nameof(PagedStyles));

    partial void OnCategoryFilterChanged(string value)
    {
        StylePage = 1;
        RefreshStylePaging();
    }

    private void OnSelectedStyleChanged(StyleItemViewModel value)
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
        if (value == WorkMode.History) RebuildHistoryItems();
    }

    // ---------- 批量处理 ----------

    public ObservableCollection<WorkItemViewModel> BatchQueue { get; } = new();

    [ObservableProperty]
    private bool _isBatchRunning;

    [ObservableProperty]
    private double _batchProgressFraction;

    public bool HasBatchQueue => BatchQueue.Count > 0;

    public string BatchProgressText
    {
        get
        {
            int done = BatchQueue.Count(q => q.BatchState is BatchItemState.Done or BatchItemState.Failed);
            return IsBatchRunning
                ? string.Format(Tr.Get("Stylize.Batch.Running"), done, BatchQueue.Count)
                : string.Format(Tr.Get("Stylize.Batch.Summary"), done, BatchQueue.Count);
        }
    }

    partial void OnIsBatchRunningChanged(bool value)
    {
        OnPropertyChanged(nameof(BatchProgressText));
        StartBatchCommand.NotifyCanExecuteChanged();
    }

    private bool CanStartBatch() => !IsBatchRunning && BatchQueue.Count > 0 && SelectedStyle is not null;

    /// <summary>批量队列入队(点击多选):RelayCommand 不能带 string 参数给 string[] 命令。</summary>
    [RelayCommand]
    private Task AddBatchFromPickerAsync() => AddBatchFilesCoreAsync(_import.PickFiles());

    /// <summary>批量队列入队(拖拽直接传路径)。</summary>
    public async Task AddBatchFilesAsync(string[]? paths)
    {
        try
        {
            await AddBatchFilesCoreAsync(paths);
        }
        catch (Exception ex)
        {
            ShowError("Stylize.Import.Failed", ex);
        }
    }

    private async Task AddBatchFilesCoreAsync(string[]? paths)
    {
        if (paths is null || paths.Length == 0) return;

        try
        {
            int cap = _settings.Current.Performance.UsePreviewQualityDuringInteraction
                ? _settings.Current.ImageProcessing.PreviewMaxDimension
                : _settings.Current.ImageProcessing.ExportMaxDimension;

            foreach (var path in paths)
            {
                if (BatchQueue.Any(q => string.Equals(q.FileName, path, StringComparison.OrdinalIgnoreCase))) continue;
                var buffer = await _import.LoadFromFileAsync(path, cap);
                var item = new WorkItemViewModel(path, buffer, _import.ToImageSource(buffer));
                BatchQueue.Add(item);
            }
            OnPropertyChanged(nameof(HasBatchQueue));
            OnPropertyChanged(nameof(BatchProgressText));
            StartBatchCommand.NotifyCanExecuteChanged();
        }
        catch (Exception ex)
        {
            ShowError("Stylize.Import.Failed", ex);
        }
    }

    [RelayCommand]
    private void RemoveFromBatch(WorkItemViewModel item)
    {
        if (IsBatchRunning) return;
        BatchQueue.Remove(item);
        OnPropertyChanged(nameof(HasBatchQueue));
        OnPropertyChanged(nameof(BatchProgressText));
        StartBatchCommand.NotifyCanExecuteChanged();
    }

    [RelayCommand]
    private void CancelBatch() => _batchCts?.Cancel();

    /// <summary>批量处理:用当前风格与参数顺序处理队列中每张图,完成后逐张入库。</summary>
    [RelayCommand(CanExecute = nameof(CanStartBatch))]
    private async Task StartBatchAsync()
    {
        var style = SelectedStyle;
        if (style is null || BatchQueue.Count == 0) return;

        int id = ++_runId;
        _batchCts?.Cancel();
        _batchCts = new CancellationTokenSource();
        var ct = _batchCts.Token;

        foreach (var q in BatchQueue) q.BatchState = BatchItemState.Pending;

        IsBatchRunning = true;
        try
        {
            var parameters = new Dictionary<string, object>();
            foreach (var p in Parameters) parameters[p.Key] = p.ToParameterValue();
            var pipeline = style.Model.BuildPipeline(parameters);

            for (int i = 0; i < BatchQueue.Count; i++)
            {
                if (ct.IsCancellationRequested) break;
                var item = BatchQueue[i];
                item.BatchState = BatchItemState.Running;
                OnPropertyChanged(nameof(BatchProgressText));

                try
                {
                    var progress = new Progress<PipelineProgress>(p =>
                    {
                        if (id != _runId) return;
                        BatchProgressFraction = p.OverallFraction;
                    });
                    var result = await _engine.ExecuteAsync(pipeline, item.SourceBuffer, parameters, !UseFullQuality, progress, ct);
                    item.ResultBuffer = result.Result;
                    item.ResultImage = _import.ToImageSource(result.Result);
                    item.IsFailed = false;
                    item.BatchState = BatchItemState.Done;
                    _ = SaveWorkAsync(item, style, result.Result);
                }
                catch (OperationCanceledException)
                {
                    item.BatchState = BatchItemState.Pending;
                    break;
                }
#pragma warning disable CA1031 // 单张失败不中断整批。
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Batch item failed: {File}", item.FileName);
                    item.IsFailed = true;
                    item.BatchState = BatchItemState.Failed;
                }
#pragma warning restore CA1031
                OnPropertyChanged(nameof(BatchProgressText));
            }
        }
        finally
        {
            IsBatchRunning = false;
            StatusText = ct.IsCancellationRequested ? Tr.Get("Stylize.Batch.Cancelled") : Tr.Get("Stylize.Batch.Finished");
        }
    }

    // ---------- 历史记录 ----------

    public ObservableCollection<HistoryItemViewModel> HistoryItems { get; } = new();

    public bool HasHistory => HistoryItems.Count > 0;

    private void RebuildHistoryItems()
    {
        HistoryItems.Clear();
        foreach (var record in _works.Works.OrderByDescending(w => w.CreatedAt).Take(30))
        {
            ImageSource? thumb = null;
            try
            {
                var path = _works.GetImageAbsolutePath(record.ResultImageFile);
                if (File.Exists(path))
                {
                    var image = new BitmapImage();
                    image.BeginInit();
                    image.CacheOption = BitmapCacheOption.OnLoad;
                    image.DecodePixelWidth = 220;
                    image.UriSource = new Uri(path);
                    image.EndInit();
                    image.Freeze();
                    thumb = image;
                }
            }
            catch
            {
                // 缩略图失败不影响列表
            }
            HistoryItems.Add(new HistoryItemViewModel(record, thumb, _works));
        }
        OnPropertyChanged(nameof(HasHistory));
    }

    /// <summary>历史记录点击 → 回到图片模式查看该作品(原图 + 当时结果)。</summary>
    [RelayCommand]
    private async Task OpenHistoryAsync(HistoryItemViewModel? item)
    {
        if (item?.Record is null) return;
        Mode = WorkMode.Image;
        await OpenWorkAsync(item.Record);
    }

    /// <summary>把一条作品记录载入查看器:原图为源,结果图直接展示(不重跑)。</summary>
    public async Task OpenWorkAsync(WorkRecord record)
    {
        try
        {
            IsBusy = true;
            StatusText = Tr.Get("Common.Loading");

            int cap = _settings.Current.Performance.UsePreviewQualityDuringInteraction
                ? _settings.Current.ImageProcessing.PreviewMaxDimension
                : _settings.Current.ImageProcessing.ExportMaxDimension;

            string? sourcePath = record.OriginalImageFile is not null ? _works.GetImageAbsolutePath(record.OriginalImageFile) : null;
            string resultPath = _works.GetImageAbsolutePath(record.ResultImageFile);
            if (sourcePath is null || !File.Exists(sourcePath)) sourcePath = File.Exists(resultPath) ? resultPath : null;
            if (sourcePath is null)
            {
                StatusText = Tr.Get("Works.Regenerate.MissingSource");
                return;
            }

            var sourceBuffer = await _import.LoadFromFileAsync(sourcePath, cap);
            var item = new WorkItemViewModel(sourcePath, sourceBuffer, _import.ToImageSource(sourceBuffer));
            _pendingTitle = record.Title;
            _pendingSourceFileName = record.SourceFileName;

            if (File.Exists(resultPath))
            {
                var resultBuffer = await _import.LoadFromFileAsync(resultPath, cap);
                item.ResultBuffer = resultBuffer;
                item.ResultImage = _import.ToImageSource(resultBuffer);
            }

            Items.Add(item);
            SelectedItem = item;
            Zoom = 1.0;
            CompareMode = item.HasResult ? CompareMode.Split : CompareMode.Original;
            StatusText = Tr.Get("Stylize.Idle");
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

    /// <summary>开始风格化(Ctrl + Enter):显式执行,完成后落一条作品记录。</summary>
    [RelayCommand(CanExecute = nameof(CanRun))]
    private Task RunAsync() => RunCoreAsync(saveWork: true);

    /// <summary>
    /// 风格化核心流程。参数/风格变化触发的自动重跑(saveWork: false)只刷新预览,
    /// 不产生作品记录 —— 否则拖一次滑条就会往作品库里灌一批重复项。
    /// </summary>
    private async Task RunCoreAsync(bool saveWork)
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

            if (saveWork)
            {
                // 显式风格化完成 → 落一条作品记录(原图 + 风格 + 参数快照)。
                _ = SaveWorkAsync(item, style, result.Result);
            }
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

    /// <summary>失败状态的 重新生成(显式操作,成功后入库)。</summary>
    [RelayCommand]
    private Task RetryAsync() => RunCoreAsync(saveWork: true);

    private async Task SaveWorkAsync(WorkItemViewModel item, StyleItemViewModel style, IImageBuffer result)
    {
        try
        {
            var record = new WorkRecord
            {
                Id = Guid.NewGuid().ToString("N"),
                Title = _pendingTitle ?? item.Title,
                SourceFileName = _pendingSourceFileName ?? Path.GetFileName(item.FileName),
                StyleId = style.Model.Id,
                StyleNameKey = style.Model.DisplayNameKey,
                StyleName = style.Name,
                CreatedAt = DateTime.Now,
                AiQuality = AiQualityMode,
            };
            foreach (var p in Parameters)
                record.Parameters.Add(new WorkParameterSnapshot { Key = p.Key, Label = p.Label, Value = p.ValueDisplay });

            _pendingTitle = null;
            _pendingSourceFileName = null;
            await _works.AddAsync(record, result, item.SourceBuffer);
            _logger.LogInformation("Work record saved: {Title}", record.Title);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to save work record");
        }
    }

    /// <summary>
    /// “我的作品 → 重新生成”:恢复原图、选中风格、套用参数快照后重跑一次。
    /// </summary>
    public async Task RestoreWorkAsync(WorkRecord record)
    {
        try
        {
            string path = record.OriginalImageFile is null ? string.Empty : _works.GetImageAbsolutePath(record.OriginalImageFile);
            if (string.IsNullOrEmpty(path) || !File.Exists(path))
            {
                StatusText = Tr.Get("Works.Regenerate.MissingSource");
                return;
            }

            var style = Styles.FirstOrDefault(s => s.Model.Id == record.StyleId) ?? SelectedStyle;
            if (style is not null)
            {
                SelectedStyle = style;
                foreach (var p in Parameters)
                {
                    var snapshot = record.Parameters.FirstOrDefault(s => s.Key == p.Key);
                    if (snapshot is null) continue;
                    if (double.TryParse(snapshot.Value, System.Globalization.CultureInfo.InvariantCulture, out var value))
                        p.SliderValue = Math.Clamp(value, p.Minimum, p.Maximum);
                }
            }

            _pendingTitle = record.Title;
            _pendingSourceFileName = record.SourceFileName;
            await ImportFilesAsync(new[] { path });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to restore work {Id}", record.Id);
        }
    }

    [RelayCommand]
    private void Cancel() => _cts?.Cancel();

    private async Task AutoRerunAsync()
    {
        if (!HasImage || SelectedStyle is null) return;
        await RunCoreAsync(saveWork: false);
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
        RefreshStylePaging();

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

/// <summary>批量队列中单张的状态。</summary>
public enum BatchItemState
{
    Pending,
    Running,
    Done,
    Failed,
}

/// <summary>历史记录条目(作品库最近记录的只读缩略)。</summary>
public sealed class HistoryItemViewModel
{
    public HistoryItemViewModel(WorkRecord record, ImageSource? thumbnail, IWorksStore works)
    {
        Record = record;
        Thumbnail = thumbnail;
        _works = works;
    }

    private readonly IWorksStore _works;

    public WorkRecord Record { get; }

    public ImageSource? Thumbnail { get; }

    public string Title => string.IsNullOrEmpty(Record.Title) ? Record.SourceFileName : Record.Title;

    public string StyleName
    {
        get
        {
            if (!string.IsNullOrEmpty(Record.StyleNameKey))
            {
                var translated = Tr.Get(Record.StyleNameKey);
                if (!string.IsNullOrEmpty(translated) && translated != Record.StyleNameKey) return translated;
            }
            return Record.StyleName;
        }
    }

    public string TimeDisplay => Record.CreatedAt.ToString("MM-dd HH:mm");

    public string ResultAbsolutePath => _works.GetImageAbsolutePath(Record.ResultImageFile);
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
