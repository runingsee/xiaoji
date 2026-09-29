namespace Huamishu.Api.Data.Entities;

/// <summary>标签表。每个用户独立拥有自己的标签。</summary>
public sealed class Tag
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
    public List<EntryTag> EntryTags { get; set; } = [];
}
