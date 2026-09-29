namespace Huamishu.Api.Data.Entities;

/// <summary>分析结果表。AI 解析后的结构化结果，可反复更新（重解析）。关联到 RawEntry。</summary>
public sealed class Entry
{
    public long Id { get; set; }
    public long RawEntryId { get; set; }
    public long UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal? Amount { get; set; }
    public string? Category { get; set; }
    /// <summary>事件发生时间（北京时间墙钟时间，支持补录）。</summary>
    public DateTime? OccurredAt { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "confirmed";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
    public RawEntry RawEntry { get; set; } = null!;
    public List<EntryTag> EntryTags { get; set; } = [];
}