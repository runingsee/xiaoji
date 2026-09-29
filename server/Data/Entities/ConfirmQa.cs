namespace Huamishu.Api.Data.Entities;

/// <summary>智能追问问答对，随条目留痕可追溯。</summary>
public sealed class ConfirmQa
{
    public long Id { get; set; }
    public long EntryId { get; set; }
    public string Question { get; set; } = "";
    public string Answer { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}