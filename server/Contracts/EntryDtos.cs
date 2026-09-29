using Huamishu.Api.AI;

namespace Huamishu.Api.Contracts;

/// <summary>口述录入请求。</summary>
public sealed record CreateEntryRequest(string RawText);

/// <summary>确认入账请求：draftToken（追问流程）与 entryId（解析失败挂起流程）二选一。</summary>
public sealed record ConfirmEntryRequest(
    string? DraftToken,
    long? EntryId,
    List<QaAnswerDto>? Answers,
    string? Clarification);

public sealed record QaAnswerDto(string Field, string Answer);

// ---------------- 口述解析响应（旧接口，保留兼容） ----------------

/// <summary>口述解析响应。</summary>
public sealed record ParseResponse(
    EntryDto? Entry = null,
    bool NeedsConfirm = false,
    string? Reason = null,
    List<MissingField>? Questions = null,
    string? DraftToken = null,
    ParseResult? Draft = null,
    string? Hint = null);

// ---------------- 批量解析响应（新接口，支持多条拆分） ----------------

/// <summary>批量解析中的单条结果。</summary>
public sealed record BatchParseResult(
    EntryDto? Entry,
    bool NeedsConfirm,
    string? Reason = null,
    List<MissingField>? Questions = null,
    string? DraftToken = null,
    ParseResult? Draft = null,
    string? Hint = null);

/// <summary>批量解析响应：包含所有拆分后的结果。</summary>
public sealed record BatchParseResponse(
    List<BatchParseResult> BatchResults,
    int TotalCount,
    string? Hint = null);

// ---------------- 条目信息 ----------------

/// <summary>条目信息。时间为北京时间墙钟时间，格式 yyyy-MM-dd / yyyy-MM-dd HH:mm。</summary>
public sealed record EntryDto(
    long Id,
    string Type,
    string Title,
    decimal? Amount,
    string? Category,
    string? OccurredAt,
    string RawText,
    string? Summary,
    string Status,
    string CreatedAt);

/// <summary>月度汇总。</summary>
public sealed record MonthSummaryDto(string Month, decimal Income, decimal Expense, decimal Balance);

/// <summary>条目修改（仅更新提供的字段；amount 传 0 无效，请直接传确切金额或 null）。</summary>
public sealed record UpdateEntryRequest(
    string? Type,
    string? Title,
    decimal? Amount,
    string? Category,
    string? OccurredAt);

/// <summary>AI 搜索请求。</summary>
public sealed record SearchRequest(string Query);

public static class EntryType
{
    public const string Expense = "expense";
    public const string Income = "income";
    public const string Event = "event";
    public const string Note = "note";

    public static readonly HashSet<string> All = [Expense, Income, Event, Note];
}
