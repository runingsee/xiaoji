namespace Huamishu.Api.Data.Entities;

/// <summary>记录-标签关联表（多对多）。</summary>
public sealed class EntryTag
{
    public long Id { get; set; }
    public long EntryId { get; set; }
    public long TagId { get; set; }
}
