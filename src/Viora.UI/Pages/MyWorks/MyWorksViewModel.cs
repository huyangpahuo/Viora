using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Viora.Core.Settings;
using Viora.Core.Works;
using Viora.UI.Localization;
using Viora.UI.Pages.Stylize;
using Viora.UI.Services;
using Viora.UI.Shell;

namespace Viora.UI.Pages.MyWorks;

/// <summary>作品卡片 + 右侧详情共用一个卡片视图模型(选中即 Inspector 内容)。</summary>
public sealed partial class WorkCardViewModel : ObservableObject
{
    public MyWorksViewModel Owner { get; }

    public WorkRecord Record { get; }

    public ImageSource? Thumbnail { get; }

    public string Title => Record.Title;

    public string StyleName => ResolveStyleName(Record);

    public string TimeDisplay => Record.CreatedAt.ToString("yyyy-MM-dd HH:mm");

    public string SizeDisplay => FormatBytes(Record.ResultBytes);

    public string DimensionsDisplay => $"{Record.ResultWidth} × {Record.ResultHeight}";

    public bool IsFavorite => Record.IsFavorite;

    public string FavoriteTooltip => Tr.Get(IsFavorite ? "Works.Card.Unfavorite" : "Works.Card.Favorite");

    /// <summary>收藏状态变化后由宿主 VM 调用:星标点亮/熄灭与悬停提示的变更通知。</summary>
    public void NotifyFavoriteChanged()
    {
        OnPropertyChanged(nameof(IsFavorite));
        OnPropertyChanged(nameof(FavoriteTooltip));
    }

    /// <summary>详情面板“作品信息”键值行。</summary>
    public IReadOnlyList<InfoRow> InfoRows { get; }

    /// <summary>详情面板“使用的参数”快照行。</summary>
    public IReadOnlyList<WorkParameterSnapshot> ParamRows => Record.Parameters;

    public sealed record InfoRow(string Label, string Value);

    public static string ResolveStyleName(WorkRecord record)
    {
        if (string.IsNullOrEmpty(record.StyleNameKey)) return record.StyleName;
        var translated = Tr.Get(record.StyleNameKey);
        return string.IsNullOrEmpty(translated) || translated == record.StyleNameKey ? record.StyleName : translated;
    }

    public static string FormatBytes(long bytes) => bytes switch
    {
        >= 1 << 20 => $"{bytes / (double)(1 << 20):0.0} MB",
        >= 1 << 10 => $"{bytes / (double)(1 << 10):0} KB",
        _ => $"{bytes} B",
    };

    public WorkCardViewModel(MyWorksViewModel owner, WorkRecord record, ImageSource? thumbnail)
        : this(owner, record, thumbnail, BuildInfoRows(record))
    {
    }

    private WorkCardViewModel(MyWorksViewModel owner, WorkRecord record, ImageSource? thumbnail, IReadOnlyList<InfoRow> infoRows)
    {
        Owner = owner;
        Record = record;
        Thumbnail = thumbnail;
        InfoRows = infoRows;
    }

    private static IReadOnlyList<InfoRow> BuildInfoRows(WorkRecord record) => new[]
    {
        new InfoRow(Tr.Get("Works.Info.Source"), record.SourceFileName),
        new InfoRow(Tr.Get("Works.Info.Style"), ResolveStyleName(record)),
        new InfoRow(Tr.Get("Works.Info.Time"), record.CreatedAt.ToString("yyyy-MM-dd HH:mm")),
        new InfoRow(Tr.Get("Works.Info.Size"), FormatBytes(record.ResultBytes)),
        new InfoRow(Tr.Get("Works.Info.Dimensions"), $"{record.ResultWidth} × {record.ResultHeight}"),
    };

    [RelayCommand]
    private void ToggleFavorite() => Owner.ToggleFavorite(this);

    [RelayCommand]
    private void Export() => Owner.ExportWork(this);

    [RelayCommand]
    private void Regenerate() => Owner.RegenerateWork(this);

    [RelayCommand]
    private void Copy() => Owner.CopyWork(this);

    [RelayCommand]
    private void Delete() => Owner.DeleteWork(this);

    [RelayCommand]
    private void OpenFolder() => Owner.OpenWorkFolder(this);

    [RelayCommand]
    private void ToggleFavoriteFromMenu() => Owner.ToggleFavorite(this);
}

public partial class MyWorksViewModel : ObservableObject
{
    private readonly IWorksStore _works;
    private readonly IExportProxy _export;
    private readonly IUiAlert _alert;
    private readonly ISettingsService _settings;
    private readonly ShellViewModel _shell;
    private readonly StylizeViewModel _stylize;
    private readonly ILogger<MyWorksViewModel> _logger;
    private readonly Dictionary<string, ImageSource?> _thumbnailCache = new();

    public MyWorksViewModel(
        IWorksStore works,
        IExportProxy export,
        IUiAlert alert,
        ISettingsService settings,
        ShellViewModel shell,
        StylizeViewModel stylize,
        ILogger<MyWorksViewModel> logger)
    {
        _works = works;
        _export = export;
        _alert = alert;
        _settings = settings;
        _shell = shell;
        _stylize = stylize;
        _logger = logger;

        TabFilters = new[]
        {
            "Works.Tab.All", "Works.Tab.Recent", "Works.Tab.Favorite", "Works.Tab.Exports",
        };
        SortOptions = new[]
        {
            "Works.Sort.Newest", "Works.Sort.Oldest", "Works.Sort.Name", "Works.Sort.Size",
        };

        _works.Changed += (_, _) => Application.Current?.Dispatcher.BeginInvoke(RebuildCards);
        LocalizationSource.Current.PropertyChanged += (_, _) => RebuildCards();

        _ = InitializeAsync();
    }

    private async Task InitializeAsync()
    {
        await _works.LoadAsync();
        Application.Current?.Dispatcher.BeginInvoke(RebuildCards);
    }

    // ---------- 筛选 / 搜索 / 排序 ----------

    public string[] TabFilters { get; }

    public string[] SortOptions { get; }

    [ObservableProperty]
    private string _tabFilter = "Works.Tab.All";

    [ObservableProperty]
    private string _searchText = string.Empty;

    [ObservableProperty]
    private string _sortKey = "Works.Sort.Newest";

    /// <summary>视图:false = 网格(默认),true = 列表。</summary>
    [ObservableProperty]
    private bool _isListView;

    public string ViewToggleTooltip => Tr.Get(IsListView ? "Works.View.Grid" : "Works.View.List");

    partial void OnIsListViewChanged(bool value) => OnPropertyChanged(nameof(ViewToggleTooltip));

    [RelayCommand]
    private void ToggleView() => IsListView = !IsListView;

    /// <summary>作品卡片每页数量。</summary>
    public const int WorksPageSize = 16;

    [ObservableProperty]
    private int _worksPage = 1;

    public System.Collections.ObjectModel.ObservableCollection<WorkCardViewModel> Cards { get; } = new();

    /// <summary>当前选中作品。公开 setter 拒绝 null:分页翻页时 ListBox 会清空选择,
    /// 详情面板应保持展示;仅 RebuildCards 内部可直接重置选中。</summary>
    public WorkCardViewModel? SelectedCard
    {
        get => _selectedCard;
        set
        {
            if (value is null) return;
            if (SetProperty(ref _selectedCard, value)) OnPropertyChanged(nameof(HasSelection));
        }
    }

    private WorkCardViewModel? _selectedCard;

    public bool HasWorks => Cards.Count > 0;

    public bool HasSelection => SelectedCard is not null;

    private List<WorkRecord> _filtered = new();

    public int WorksTotalItems => _filtered.Count;

    public int WorksTotalPages => Math.Max(1, (int)Math.Ceiling(WorksTotalItems / (double)WorksPageSize));

    public bool WorksPagerVisible => WorksTotalPages > 1;

    partial void OnTabFilterChanged(string value)
    {
        WorksPage = 1;
        RebuildCards();
    }

    partial void OnSearchTextChanged(string value)
    {
        WorksPage = 1;
        RebuildCards();
    }

    partial void OnSortKeyChanged(string value)
    {
        WorksPage = 1;
        RebuildCards();
    }

    partial void OnWorksPageChanged(int value) => RebuildCards();

    private IEnumerable<WorkRecord> Query()
    {
        IEnumerable<WorkRecord> query = _works.Works;
        query = TabFilter switch
        {
            "Works.Tab.Recent" => query.Where(w => w.CreatedAt >= DateTime.Now.AddDays(-7)),
            "Works.Tab.Favorite" => query.Where(w => w.IsFavorite),
            "Works.Tab.Exports" => query.Where(w => w.ExportedAt is not null),
            _ => query,
        };

        if (!string.IsNullOrWhiteSpace(SearchText))
        {
            string needle = SearchText.Trim();
            query = query.Where(w =>
                w.Title.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || w.SourceFileName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || w.StyleName.Contains(needle, StringComparison.OrdinalIgnoreCase)
                || WorkCardViewModel.ResolveStyleName(w).Contains(needle, StringComparison.OrdinalIgnoreCase)
                || w.Parameters.Any(p => p.Value.Contains(needle, StringComparison.OrdinalIgnoreCase)));
        }

        return SortKey switch
        {
            "Works.Sort.Oldest" => query.OrderBy(w => w.CreatedAt),
            "Works.Sort.Name" => query.OrderBy(w => w.Title, StringComparer.CurrentCultureIgnoreCase),
            "Works.Sort.Size" => query.OrderByDescending(w => w.ResultBytes),
            _ => query.OrderByDescending(w => w.CreatedAt),
        };
    }

    private void RebuildCards()
    {
        _filtered = Query().ToList();
        string? selectedId = SelectedCard?.Record.Id;
        bool selectedStillExists = _filtered.Any(w => w.Id == selectedId);

        int page = Math.Clamp(WorksPage, 1, WorksTotalPages);
        var slice = _filtered.Skip((page - 1) * WorksPageSize).Take(WorksPageSize);

        Cards.Clear();
        foreach (var record in slice)
            Cards.Add(new WorkCardViewModel(this, record, GetThumbnail(record)));

        // 选中仍存在(哪怕被翻到别的页)→ 保持详情;否则落到第一张(空库为 null)。
        var nextSelected = selectedStillExists
            ? Cards.FirstOrDefault(c => c.Record.Id == selectedId) ?? _selectedCard
            : Cards.FirstOrDefault();
        _selectedCard = nextSelected;
        OnPropertyChanged(nameof(SelectedCard));
        OnPropertyChanged(nameof(HasSelection));
        OnPropertyChanged(nameof(HasWorks));
        OnPropertyChanged(nameof(WorksTotalItems));
        OnPropertyChanged(nameof(WorksTotalPages));
        OnPropertyChanged(nameof(WorksPagerVisible));
    }

    private ImageSource? GetThumbnail(WorkRecord record)
    {
        if (string.IsNullOrEmpty(record.ResultImageFile)) return null;
        if (_thumbnailCache.TryGetValue(record.Id, out var cached)) return cached;

        try
        {
            var path = _works.GetImageAbsolutePath(record.ResultImageFile);
            if (!File.Exists(path)) return null;

            var image = new BitmapImage();
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.DecodePixelWidth = 440;
            image.UriSource = new Uri(path);
            image.EndInit();
            image.Freeze();
            _thumbnailCache[record.Id] = image;
            return image;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Thumbnail load failed for {File}", record.ResultImageFile);
            return null;
        }
    }

    // ---------- 操作 ----------

    public void ToggleFavorite(WorkCardViewModel card)
    {
        card.Record.IsFavorite = !card.Record.IsFavorite;
        card.NotifyFavoriteChanged(); // IsFavorite 是计算属性,需显式通知星标才会点亮/熄灭
        _ = UpdateAsync(card.Record);
        if (TabFilter == "Works.Tab.Favorite") RebuildCards(); // 收藏 Tab 实时增减
    }

    public void ExportWork(WorkCardViewModel card)
    {
        var record = card.Record;
        try
        {
            var sourcePath = _works.GetImageAbsolutePath(record.ResultImageFile);
            if (!File.Exists(sourcePath)) throw new FileNotFoundException(sourcePath);

            var target = _export.PickSavePath("png");
            if (string.IsNullOrEmpty(target)) return;

            File.Copy(sourcePath, target, overwrite: true);
            record.ExportedAt = DateTime.Now;
            _ = UpdateAsync(record);
            _alert.Info(string.Format(Tr.Get("Works.Export.Saved"), target));
            _logger.LogInformation("Work {Id} exported to {Path}", record.Id, target);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Export failed for {Id}", record.Id);
            _alert.Warn(Tr.Get("Works.Export.Failed"), ex.Message);
        }
    }

    public void RegenerateWork(WorkCardViewModel card)
    {
        _shell.NavigateTo("Nav.Stylize");
        _ = _stylize.RestoreWorkAsync(card.Record);
    }

    public void CopyWork(WorkCardViewModel card) => _ = _works.CopyAsync(card.Record);

    public void DeleteWork(WorkCardViewModel card)
    {
        var record = card.Record;
        string body = string.Format(Tr.Get("Works.Delete.Confirm.Body"), record.Title);
        if (!_alert.Confirm(Tr.Get("Works.Delete.Confirm.Title"), body)) return;

        _thumbnailCache.Remove(record.Id);
        _ = _works.DeleteAsync(record);
    }

    public void OpenWorkFolder(WorkCardViewModel card)
    {
        var folder = _works.WorksFolder;
        if (!Directory.Exists(folder)) Directory.CreateDirectory(folder);
        Process.Start("explorer.exe", $"/select,\"{_works.GetImageAbsolutePath(card.Record.ResultImageFile)}\"");
    }

    [RelayCommand]
    private void GoStylize() => _shell.NavigateTo("Nav.Stylize");

    /// <summary>详情面板标题编辑提交(TextBox LostFocus 调用)。</summary>
    public void CommitTitleEdit()
    {
        if (SelectedCard is null) return;
        var record = SelectedCard.Record;
        if (string.IsNullOrWhiteSpace(record.Title)) record.Title = Path.GetFileNameWithoutExtension(record.SourceFileName);
        _ = UpdateAsync(record);
    }

    private async Task UpdateAsync(WorkRecord record)
    {
        try
        {
            await _works.UpdateAsync(record);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Work update failed for {Id}", record.Id);
        }
    }
}
