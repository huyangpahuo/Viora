using Viora.Core.Imaging;

namespace Viora.Core.Works;

/// <summary>一个参数快照项:作品创建时的参数取值(只读展示,不是实时编辑器)。</summary>
public sealed class WorkParameterSnapshot
{
    public string Key { get; set; } = string.Empty;

    /// <summary>保存时的本地化标签(旧记录);新记录请同时写 DisplayNameKey 供显示时重本地化。</summary>
    public string Label { get; set; } = string.Empty;

    /// <summary>标签的本地化键(如 Param.Generic.Density);可空(旧记录)。</summary>
    public string? DisplayNameKey { get; set; }

    public string Value { get; set; } = string.Empty;
}

/// <summary>
/// 作品记录 —— Viora 自己的作品数据库条目(作品 ≠ 文件):
/// 记录原图来源、所用风格、参数快照与结果图片,支撑“我的作品”的管理能力。
/// </summary>
public sealed class WorkRecord
{
    public string Id { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>显示用原始文件名(如 湖边小镇.jpg)。</summary>
    public string SourceFileName { get; set; } = string.Empty;

    public string StyleId { get; set; } = string.Empty;

    /// <summary>风格显示名的本地化键;旧记录/已卸载插件回退到 StyleName。</summary>
    public string StyleNameKey { get; set; } = string.Empty;

    public string StyleName { get; set; } = string.Empty;

    /// <summary>结果图片文件名(位于 WorksFolder 内)。</summary>
    public string ResultImageFile { get; set; } = string.Empty;

    /// <summary>原始图片文件名(用于“重新生成”);可为空。</summary>
    public string? OriginalImageFile { get; set; }

    public int ResultWidth { get; set; }

    public int ResultHeight { get; set; }

    public long ResultBytes { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ExportedAt { get; set; }

    public bool IsFavorite { get; set; }

    public bool AiQuality { get; set; }

    public List<WorkParameterSnapshot> Parameters { get; set; } = new();
}

/// <summary>
/// 作品存储:图片落盘到作品文件夹,元数据持久化为 JSON 索引。
/// </summary>
public interface IWorksStore
{
    /// <summary>任何增删改后触发(UI 侧负责切换到自己的线程)。</summary>
    event EventHandler? Changed;

    IReadOnlyList<WorkRecord> Works { get; }

    string WorksFolder { get; }

    /// <summary>重定向作品库目录(须在 LoadAsync 之前调用;空串 = 恢复默认)。</summary>
    void SetWorksFolder(string absolutePath);

    Task LoadAsync(CancellationToken cancellationToken = default);

    /// <summary>写入结果图(与可选原图)并追加记录。</summary>
    Task AddAsync(WorkRecord record, IImageBuffer result, IImageBuffer? original, CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkRecord record, CancellationToken cancellationToken = default);

    /// <summary>基于现有作品创建副本(复制图片文件,标题加“副本”)。</summary>
    Task CopyAsync(WorkRecord record, CancellationToken cancellationToken = default);

    Task DeleteAsync(WorkRecord record, CancellationToken cancellationToken = default);

    string GetImageAbsolutePath(string fileName);
}
