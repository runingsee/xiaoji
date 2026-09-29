using System.Text.Json.Serialization;

namespace Huamishu.Api.AI;

/// <summary>DeepSeek 对话消息。</summary>
public sealed record ChatMessage(string Role, string Content);

/// <summary>解析结果中要求追问的字段。</summary>
public sealed record MissingField(string Field, string Question);

/// <summary>
/// 口述解析结果（与 Prompts.ParseSystem 中约定的 JSON 结构一一对应）。
/// OccurredAt 为 "yyyy-MM-dd" 字符串，由调用方转为 DateTime。
/// </summary>
public sealed record ParseResult(
    string Type,
    string Title,
    decimal? Amount,
    string? Category,
    string? OccurredAt,
    string Summary,
    List<MissingField>? Missing)
{
    public List<MissingField> MissingList => Missing ?? [];
}

/// <summary>对话查询时提供给模型的"已知事实"条目（RAG-lite）。</summary>
public sealed record ChatFact(string Kind, string Label, string Value);

/// <summary>查询意图解析结果（与 Prompts.SearchIntentPrompt 约定的 JSON 结构一一对应）。</summary>
public sealed record SearchIntent(
    string Type,
    DateRange? DateRange,
    string? Keyword,
    double Confidence);

/// <summary>查询日期范围。</summary>
public sealed record DateRange(string Start, string End);