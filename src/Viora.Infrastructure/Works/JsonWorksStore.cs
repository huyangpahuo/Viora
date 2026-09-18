using System.IO;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Viora.Core.Imaging;
using Viora.Core.Plugins;
using Viora.Core.Works;
using Viora.Infrastructure.Export;
using Viora.Infrastructure.Logging;

namespace Viora.Infrastructure.Works;

/// <summary>
/// JSON 索引 + 图片落盘的作品存储:works.json 记录元数据,图片按 {id}.png(结果)
/// 与 {id}-source.jpg(原图)写入 %LOCALAPPDATA%\Viora\works\。线程安全(信号量)。
/// </summary>
public sealed class JsonWorksStore : IWorksStore
{
    private static readonly JsonSerializerOptions JsonOpts = new() { WriteIndented = true };

    private readonly SemaphoreSlim _gate = new(1, 1);
    private readonly List<WorkRecord> _works = new();
    private readonly ILogger<JsonWorksStore> _logger;

    public JsonWorksStore(IAppPaths paths, ILogger<JsonWorksStore> logger)
    {
        WorksFolder = Path.Combine(paths.Root, "works");
        _logger = logger;
    }

    public string WorksFolder { get; }

    public string IndexFile => Path.Combine(WorksFolder, "works.json");

    public event EventHandler? Changed;

    public IReadOnlyList<WorkRecord> Works { get { lock (_works) return _works.ToList(); } }

    public string GetImageAbsolutePath(string fileName) => Path.Combine(WorksFolder, fileName);

    public async Task LoadAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            if (!File.Exists(IndexFile))
            {
                lock (_works) _works.Clear();
                return;
            }

            await using var stream = File.OpenRead(IndexFile);
            var records = await JsonSerializer.DeserializeAsync<List<WorkRecord>>(stream, JsonOpts, cancellationToken);
            lock (_works)
            {
                _works.Clear();
                if (records is not null) _works.AddRange(records);
            }
            _logger.LogInformation("Loaded {Count} work records", _works.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to load works index");
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task AddAsync(WorkRecord record, IImageBuffer result, IImageBuffer? original, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            Directory.CreateDirectory(WorksFolder);

            record.ResultImageFile = $"{record.Id}.png";
            record.ResultWidth = result.Width;
            record.ResultHeight = result.Height;
            await WriteImageAsync(new PngExporter(), record.ResultImageFile, result, new Dictionary<string, object>(), cancellationToken);
            record.ResultBytes = new FileInfo(GetImageAbsolutePath(record.ResultImageFile)).Length;

            if (original is not null)
            {
                record.OriginalImageFile = $"{record.Id}-source.jpg";
                await WriteImageAsync(new JpegExporter(), record.OriginalImageFile, original,
                    new Dictionary<string, object> { ["quality"] = 92 }, cancellationToken);
            }

            lock (_works) _works.Add(record);
            await SaveIndexAsync(cancellationToken);
            OnChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task UpdateAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            lock (_works)
            {
                int index = _works.FindIndex(w => w.Id == record.Id);
                if (index < 0) return;
                _works[index] = record;
            }
            await SaveIndexAsync(cancellationToken);
            OnChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task CopyAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var copy = Clone(record);
            copy.Id = Guid.NewGuid().ToString("N");
            copy.Title = record.Title + " - 副本";
            copy.CreatedAt = DateTime.Now;
            copy.IsFavorite = false;
            copy.ExportedAt = null;

            copy.ResultImageFile = $"{copy.Id}.png";
            File.Copy(GetImageAbsolutePath(record.ResultImageFile), GetImageAbsolutePath(copy.ResultImageFile), overwrite: true);
            copy.ResultBytes = new FileInfo(GetImageAbsolutePath(copy.ResultImageFile)).Length;

            if (record.OriginalImageFile is not null && File.Exists(GetImageAbsolutePath(record.OriginalImageFile)))
            {
                copy.OriginalImageFile = $"{copy.Id}-source.jpg";
                File.Copy(GetImageAbsolutePath(record.OriginalImageFile), GetImageAbsolutePath(copy.OriginalImageFile), overwrite: true);
            }

            lock (_works) _works.Add(copy);
            await SaveIndexAsync(cancellationToken);
            OnChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    public async Task DeleteAsync(WorkRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            foreach (var file in new[] { record.ResultImageFile, record.OriginalImageFile })
            {
                if (file is null) continue;
                var path = GetImageAbsolutePath(file);
                if (File.Exists(path)) File.Delete(path);
            }

            lock (_works) _works.RemoveAll(w => w.Id == record.Id);
            await SaveIndexAsync(cancellationToken);
            OnChanged();
        }
        finally
        {
            _gate.Release();
        }
    }

    private async Task WriteImageAsync(
        IImageExporter exporter, string fileName, IImageBuffer buffer,
        IReadOnlyDictionary<string, object> options, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(GetImageAbsolutePath(fileName));
        await exporter.ExportAsync(buffer, stream, options, cancellationToken);
    }

    private async Task SaveIndexAsync(CancellationToken cancellationToken)
    {
        List<WorkRecord> snapshot;
        lock (_works) snapshot = _works.ToList();
        Directory.CreateDirectory(WorksFolder);
        await using var stream = File.Create(IndexFile);
        await JsonSerializer.SerializeAsync(stream, snapshot, JsonOpts, cancellationToken);
    }

    private static WorkRecord Clone(WorkRecord record) => new()
    {
        Id = record.Id,
        Title = record.Title,
        SourceFileName = record.SourceFileName,
        StyleId = record.StyleId,
        StyleNameKey = record.StyleNameKey,
        StyleName = record.StyleName,
        ResultImageFile = record.ResultImageFile,
        OriginalImageFile = record.OriginalImageFile,
        ResultWidth = record.ResultWidth,
        ResultHeight = record.ResultHeight,
        ResultBytes = record.ResultBytes,
        CreatedAt = record.CreatedAt,
        ExportedAt = record.ExportedAt,
        IsFavorite = record.IsFavorite,
        AiQuality = record.AiQuality,
        Parameters = record.Parameters.Select(p => new WorkParameterSnapshot
        {
            Key = p.Key, Label = p.Label, Value = p.Value,
        }).ToList(),
    };

    private void OnChanged()
    {
        try { Changed?.Invoke(this, EventArgs.Empty); }
#pragma warning disable CA1031 // 存储层边界:订阅者异常不应破坏存储操作。
        catch (Exception ex) { _logger.LogError(ex, "Works.Changed handler failed"); }
#pragma warning restore CA1031
    }
}
