namespace Huamishu.Api.Contracts;

/// <summary>创建标签请求。</summary>
public sealed record CreateTagRequest(string Name);

/// <summary>标签信息。</summary>
public sealed record TagDto(long Id, string Name, string CreatedAt);

/// <summary>批量为记录添加标签请求。</summary>
public sealed record AddTagToEntryRequest(long EntryId, List<long> TagIds);

/// <summary>标签统计项：按标签 + 类型分组（收入/支出），count 为记录数，totalAmount 为金额合计。</summary>
public sealed record TagStatsDto(string TagName, int Count, decimal TotalAmount, string Type);
