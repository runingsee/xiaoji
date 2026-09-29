namespace Huamishu.Api.Data.Entities;

/// <summary>原始记录表。用户口述的原始文本，永久保留，永不修改。</summary>
public sealed class RawEntry
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string RawText { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}
