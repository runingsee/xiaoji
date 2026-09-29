using Huamishu.Api.AI;
using Huamishu.Api.Contracts;
using Huamishu.Api.Data;
using Microsoft.EntityFrameworkCore;

namespace Huamishu.Api.Services;

/// <summary>
/// 对话式咨询（RAG-lite）：
/// 先按用户问题涉及的维度从 MySQL 检索结构化数据 → 组装为 Facts → DeepSeek 仅依据 Facts 作答。
/// V1 不引入向量库。
/// </summary>
public sealed class AskService(AppDbContext db, DeepSeekClient ai)
{
    private const int RecentCount = 15;
    private const int UpcomingCount = 10;

    /// <summary>EF 投影目标（保证 Select 可翻译成 SQL）。</summary>
    private sealed record MonthRow(string Type, decimal? Amount, string? Category, string Title);

    public async Task<AskResponse> AskAsync(long userId, string question, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(question))
            throw new Common.ApiException(400, "总得问点啥吧");

        var now = DateTime.Now;
        var monthStart = new DateTime(now.Year, now.Month, 1);
        var monthEnd = monthStart.AddMonths(1);

        var monthRows = await db.Entries
            .Where(e => e.UserId == userId
                        && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd
                        && (e.Type == EntryType.Income || e.Type == EntryType.Expense))
            .Select(e => new MonthRow(e.Type, e.Amount, e.Category, e.Title))
            .ToListAsync(ct);

        var recent = await db.Entries
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.OccurredAt)
            .Take(RecentCount)
            .ToListAsync(ct);

        var upcoming = await db.Entries
            .Where(e => e.UserId == userId && e.Type == EntryType.Event && e.OccurredAt >= now)
            .OrderBy(e => e.OccurredAt)
            .Take(UpcomingCount)
            .ToListAsync(ct);

        var facts = BuildFacts(now, monthStart, monthRows, recent, upcoming);
        var answer = await ai.AskAsync(facts, question, ct);

        return new AskResponse(answer ?? "我这边暂时没算出来，稍后再试试吧。");
    }

    private static List<ChatFact> BuildFacts(
        DateTime now, DateTime monthStart,
        List<MonthRow> monthRows,
        List<Data.Entities.Entry> recent, List<Data.Entities.Entry> upcoming)
    {
        var facts = new List<ChatFact>();

        var income = monthRows.Where(r => r.Type == EntryType.Income).Sum(r => r.Amount ?? 0);
        var expense = monthRows.Where(r => r.Type == EntryType.Expense).Sum(r => r.Amount ?? 0);
        facts.Add(new ChatFact("month_summary", $"本月（{monthStart:yyyy-MM}）",
            $"收入 {income:0.##} 元，支出 {expense:0.##} 元，结余 {income - expense:0.##} 元"));

        facts.AddRange(GroupByCategory("income", monthRows.Where(r => r.Type == EntryType.Income)));
        facts.AddRange(GroupByCategory("expense", monthRows.Where(r => r.Type == EntryType.Expense)));

        foreach (var e in recent)
        {
            facts.Add(new ChatFact("record",
                $"{e.Title}（{e.Type}）",
                $"{e.OccurredAt:yyyy-MM-dd}  {(e.Amount is not null ? e.Amount.Value.ToString("0.##") + " 元" : "")} 分类：{(e.Category ?? "未分类")}"));
        }

        foreach (var e in upcoming)
        {
            facts.Add(new ChatFact("schedule",
                $"{e.Title}",
                $"{e.OccurredAt:yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(e.Summary) ? "" : "，" + e.Summary)}"));
        }

        return facts;
    }

    private static IEnumerable<ChatFact> GroupByCategory(string kind, IEnumerable<MonthRow> rows) =>
        rows.GroupBy(r => r.Category ?? "未分类")
            .Select(g => new ChatFact($"{kind}_by_category", g.Key,
                $"共 {g.Sum(r => r.Amount ?? 0):0.##} 元（{g.Count()} 笔）"));
}