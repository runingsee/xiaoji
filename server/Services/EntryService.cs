using Huamishu.Api.AI;
using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Data;
using Huamishu.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;

namespace Huamishu.Api.Services;

/// <summary>
/// 口述录入核心链路：
///   1. ParseAsync —— AI 解析；信息完整 → 直接入库；信息缺失 → 发追问 + 临时草稿票；
///      AI 挂了 → 原样挂起（status=pending），等用户确认时再重解析。
///   2. ConfirmAsync —— 用户补充回答后合并重解析 → 正式入库（追问留痕 confirm_qa）。
/// 草稿暂存于内存（IMemoryCache，10 分钟有效，重启即失效——草稿本就该是瞬态的）。
/// </summary>
public sealed class EntryService(AppDbContext db, DeepSeekClient ai, IMemoryCache drafts)
{
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(10);
    private const int ListCap = 200;

    private sealed record DraftState(string RawText, ParseResult Result, List<QaAnswerDto> Answers);

    // ---------------------------------------------------------------- 解析

    public async Task<ParseResponse> ParseAsync(long userId, string rawText, CancellationToken ct)
    {
        rawText = rawText.Trim();
        if (string.IsNullOrEmpty(rawText))
            throw ApiException.BadRequest("请先说点什么再记");

        // 一次失败重试
        var result = await ai.ParseAsync(rawText, ct) ?? await ai.ParseAsync(rawText, ct);

        // AI 不可用：原样挂起为 pending，确认时再重解析
        if (result is null)
        {
            var entry = await InsertStubAsync(userId, rawText, EntryType.Note, ct);
            return new ParseResponse(
                ToDto(entry, entry.RawEntry.RawText), NeedsConfirm: true, Reason: "parse_failed",
                Hint: "AI 暂时没接上，这条我先原样挂起来了，你可以稍后在列表里补充修正。");
        }

        // 信息完整：直接入库
        if (result.MissingList.Count == 0)
        {
            var entry = await InsertAsync(userId, rawText, result, answers: null, ct);
            return new ParseResponse(ToDto(entry, entry.RawEntry.RawText), NeedsConfirm: false);
        }

        // 信息缺失：发追问 + 草稿票
        var token = Guid.NewGuid().ToString("N");
        drafts.Set(token, new DraftState(rawText, result, []), DraftTtl);
        return new ParseResponse(
            null, NeedsConfirm: true, Reason: "missing",
            Questions: result.MissingList, DraftToken: token, Draft: result,
            Hint: "还有几个信息没弄清楚，帮我补齐一下就入账。");
    }

    // ---------------------------------------------------------------- 批量解析

    /// <summary>
    /// 批量解析：先按规则拆分文本，再逐段 AI 解析，各自入库。
    /// 返回所有拆分段的结果。
    /// </summary>
    public async Task<BatchParseResponse> BatchParseAsync(long userId, string rawText, CancellationToken ct)
    {
        rawText = rawText.Trim();
        if (string.IsNullOrEmpty(rawText))
            throw ApiException.BadRequest("请先说点什么再记");

        var segments = TextSplitter.Split(rawText);
        var batchResults = new List<BatchParseResult>(segments.Count);

        foreach (var segment in segments)
        {
            var result = await ai.ParseAsync(segment, ct) ?? await ai.ParseAsync(segment, ct);

            if (result is null)
            {
                var entry = await InsertStubAsync(userId, segment, EntryType.Note, ct);
                batchResults.Add(new BatchParseResult(
                    ToDto(entry, entry.RawEntry.RawText), NeedsConfirm: true, Reason: "parse_failed",
                    Hint: "AI 暂时没接上，这条我先原样挂起来了。"));
            }
            else if (result.MissingList.Count == 0)
            {
                var entry = await InsertAsync(userId, segment, result, answers: null, ct);
                batchResults.Add(new BatchParseResult(ToDto(entry, entry.RawEntry.RawText), NeedsConfirm: false));
            }
            else
            {
                var token = Guid.NewGuid().ToString("N");
                drafts.Set(token, new DraftState(segment, result, []), DraftTtl);
                batchResults.Add(new BatchParseResult(
                    null, NeedsConfirm: true, Reason: "missing",
                    Questions: result.MissingList, DraftToken: token, Draft: result,
                    Hint: "还有几个信息没弄清楚，帮我补齐一下就入账。"));
            }
        }

        return new BatchParseResponse(batchResults, batchResults.Count);
    }

    // ---------------------------------------------------------------- AI 搜索

    /// <summary>AI 搜索：先解析用户自然语言查询为结构化条件，再查询数据库。</summary>
    public async Task<List<EntryDto>> SearchAsync(long userId, string query, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(query))
            throw ApiException.BadRequest("请输入搜索内容");

        var intent = await ai.ParseSearchIntentAsync(query, ct);

        var q = db.Entries.Where(e => e.UserId == userId).AsQueryable();

        if (intent is not null)
        {
            if (!string.IsNullOrWhiteSpace(intent.Type) && EntryType.All.Contains(intent.Type))
                q = q.Where(e => e.Type == intent.Type);

            if (intent.DateRange is not null)
            {
                if (DateTime.TryParseExact(intent.DateRange.Start, "yyyy-MM-dd",
                        System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var start))
                {
                    var end = DateTime.TryParseExact(intent.DateRange.End, "yyyy-MM-dd",
                            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var e)
                        ? e.AddMonths(1)
                        : new DateTime(start.Year, start.Month, 1).AddMonths(1);
                    q = q.Where(e => e.OccurredAt >= start && e.OccurredAt < end);
                }
            }

            if (!string.IsNullOrWhiteSpace(intent.Keyword))
            {
                var like = $"%{intent.Keyword.Trim()}%";
                q = q.Where(e =>
                    EF.Functions.Like(e.Title, like) ||
                    EF.Functions.Like(e.Category ?? "", like) ||
                    EF.Functions.Like(e.RawEntry.RawText, like) ||
                    EF.Functions.Like(e.Summary ?? "", like));
            }
        }

        var rows = await q
            .Include(e => e.RawEntry)
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(ListCap)
            .ToListAsync(ct);

        return rows.Select(e => ToDto(e, e.RawEntry.RawText)).ToList();
    }

    // ---------------------------------------------------------------- 确认

    public async Task<ParseResponse> ConfirmAsync(long userId, ConfirmEntryRequest req, CancellationToken ct)
    {
        var answers = req.Answers ?? [];

        // 追问流程：草稿票 + 追加的回答
        if (!string.IsNullOrEmpty(req.DraftToken))
        {
            if (!drafts.TryGetValue(req.DraftToken, out DraftState? draft) || draft is null)
                throw ApiException.BadRequest("这笔录入已过期，请重新口述一遍");

            var accumulated = draft.Answers.Concat(answers).ToList();
            var mergedText = MergeText(draft.RawText, accumulated);
            var result = await ai.ParseAsync(mergedText, ct) ?? await ai.ParseAsync(mergedText, ct);

            if (result is null) // 重解析仍失败：兜底按 note 入库
            {
                var entry = await InsertStubAsync(userId, draft.RawText, EntryType.Note, ct);
                drafts.Remove(req.DraftToken);
                return new ParseResponse(ToDto(entry, entry.RawEntry.RawText), NeedsConfirm: true, Reason: "parse_failed",
                    Hint: "AI 一直没接上，先把原文挂起来了，之后可人工修正。");
            }

            if (result.MissingList.Count > 0) // 还有缺失：继续追问（草稿续期）
            {
                drafts.Set(req.DraftToken, draft with { Result = result, Answers = accumulated }, DraftTtl);
                return new ParseResponse(
                    null, NeedsConfirm: true, Reason: "missing",
                    Questions: result.MissingList, DraftToken: req.DraftToken, Draft: result,
                    Hint: "还差一点点信息，再补一下就成。");
            }

            drafts.Remove(req.DraftToken);
            var confirmed = await InsertAsync(userId, draft.RawText, result, accumulated, ct);
            return new ParseResponse(ToDto(confirmed, confirmed.RawEntry.RawText), NeedsConfirm: false);
        }

        // 挂起流程：解析失败时产生的 pending 条目 + 澄清文本
        if (req.EntryId is long id)
        {
            var entry = await db.Entries.FirstOrDefaultAsync(
                e => e.Id == id && e.UserId == userId && e.Status == "pending", ct)
                ?? throw ApiException.NotFound("记录不存在");

            var mergedText = string.IsNullOrWhiteSpace(req.Clarification)
                ? entry.RawEntry.RawText
                : entry.RawEntry.RawText + "，补充说明：" + req.Clarification.Trim();

            var result = await ai.ParseAsync(mergedText, ct) ?? await ai.ParseAsync(mergedText, ct);

            if (result is not null && result.MissingList.Count == 0)
            {
                ApplyResult(entry, result);
                entry.Status = "confirmed";
            }
            else
            {
                // 解析失败或有缺失：原样转为 note 兜底，不再无限循环追问
                entry.Type = EntryType.Note;
                entry.Title = Truncate(entry.RawEntry.RawText, 40);
                entry.Amount = null;
                entry.Category = null;
                entry.Summary = null;
                entry.Status = "confirmed";
            }
            if (answers.Count > 0)
                db.ConfirmQas.AddRange(answers.Select(a => new ConfirmQa
                {
                    EntryId = entry.Id, Question = a.Field, Answer = a.Answer, CreatedAt = DateTime.Now,
                }));
            await db.SaveChangesAsync(ct);

            var dto = ToDto(entry, entry.RawEntry.RawText);
            return new ParseResponse(dto, NeedsConfirm: false,
                Hint: result is null ? "AI 仍不可用，已按原文记录，之后可手动修正。" : null);
        }

        throw ApiException.BadRequest("缺少有效的确认凭据");
    }

    // ---------------------------------------------------------------- 查询

    public async Task<List<EntryDto>> ListAsync(long userId, string? type, string? month, string? q, long? tagId, CancellationToken ct)
    {
        var query = db.Entries.Where(e => e.UserId == userId).AsQueryable();

        if (!string.IsNullOrWhiteSpace(type) && EntryType.All.Contains(type!))
            query = query.Where(e => e.Type == type);

        if (!string.IsNullOrWhiteSpace(month) && DateTime.TryParseExact(month, "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var m))
        {
            var next = m.AddMonths(1);
            query = query.Where(e => e.OccurredAt >= m && e.OccurredAt < next);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(e =>
                EF.Functions.Like(e.Title, like) ||
                EF.Functions.Like(e.Category ?? "", like) ||
                EF.Functions.Like(e.RawEntry.RawText, like) ||
                EF.Functions.Like(e.Summary ?? "", like));
        }

        // 标签过滤（想法三）：仅当标签确属当前用户时生效，否则返回空列表
        if (tagId is long tid)
        {
            var tagBelongsToUser = await db.Tags.AnyAsync(t => t.Id == tid && t.UserId == userId, ct);
            query = query.Where(e => tagBelongsToUser && e.EntryTags.Any(et => et.TagId == tid));
        }

        var rows = await query
            .Include(e => e.RawEntry)
            .OrderByDescending(e => e.OccurredAt)
            .ThenByDescending(e => e.Id)
            .Take(ListCap)
            .ToListAsync(ct);

        return rows.Select(e => ToDto(e, e.RawEntry.RawText)).ToList();
    }

    public async Task<MonthSummaryDto> SummaryAsync(long userId, string? month, CancellationToken ct)
    {
        var m = DateTime.TryParseExact(month, "yyyy-MM",
            System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var next = m.AddMonths(1);

        var rows = await db.Entries
            .Where(e => e.UserId == userId
                        && e.OccurredAt >= m && e.OccurredAt < next
                        && (e.Type == EntryType.Income || e.Type == EntryType.Expense))
            .Select(e => new { e.Type, e.Amount })
            .ToListAsync(ct);

        var income = rows.Where(r => r.Type == EntryType.Income).Sum(r => r.Amount ?? 0);
        var expense = rows.Where(r => r.Type == EntryType.Expense).Sum(r => r.Amount ?? 0);
        return new MonthSummaryDto(m.ToString("yyyy-MM"), income, expense, income - expense);
    }

    public async Task<EntryDto?> UpdateAsync(long userId, long id, UpdateEntryRequest req, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct)
            ?? throw ApiException.NotFound("记录不存在");

        if (req.Type is not null)
        {
            if (!EntryType.All.Contains(req.Type))
                throw ApiException.BadRequest("非法的记录类型");
            entry.Type = req.Type;
        }
        if (!string.IsNullOrWhiteSpace(req.Title)) entry.Title = req.Title.Trim();
        if (req.Amount is not null) entry.Amount = req.Amount;
        if (req.Category is not null) entry.Category = string.IsNullOrWhiteSpace(req.Category) ? null : req.Category.Trim();
        if (req.OccurredAt is not null)
        {
            entry.OccurredAt = TimeFormat.TryParseOccurredAt(req.OccurredAt)
                ?? throw ApiException.BadRequest("时间格式不正确");
        }

        await db.SaveChangesAsync(ct);
        return ToDto(entry, entry.RawEntry.RawText);
    }

    public async Task<bool> DeleteAsync(long userId, long id, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(e => e.Id == id && e.UserId == userId, ct);
        if (entry is null) return false;
        db.Entries.Remove(entry);
        await db.SaveChangesAsync(ct);
        return true;
    }

    // ---------------------------------------------------------------- 私有

    /// <summary>信息完整时正式入库，并写入追问留痕。</summary>
    private async Task<Entry> InsertAsync(long userId, string rawText, ParseResult result,
        List<QaAnswerDto>? answers, CancellationToken ct)
    {
        var rawEntry = new RawEntry { UserId = userId, RawText = rawText, Status = "pending", CreatedAt = DateTime.Now };
        db.RawEntries.Add(rawEntry);
        await db.SaveChangesAsync(ct);

        var entry = new Entry { RawEntryId = rawEntry.Id, UserId = userId, Status = "confirmed", CreatedAt = DateTime.Now };
        ApplyResult(entry, result);

        db.Entries.Add(entry);
        if (answers is { Count: > 0 })
        {
            db.ConfirmQas.AddRange(answers.Select(a => new ConfirmQa
            {
                EntryId = entry.Id, Question = a.Field, Answer = a.Answer, CreatedAt = DateTime.Now,
            }));
        }
        await db.SaveChangesAsync(ct);
        return entry;
    }

    /// <summary>AI 不可用时的兜底：原文挂起，待确认重解析。</summary>
    private async Task<Entry> InsertStubAsync(long userId, string rawText, string type, CancellationToken ct)
    {
        var rawEntry = new RawEntry { UserId = userId, RawText = rawText, Status = "pending", CreatedAt = DateTime.Now };
        db.RawEntries.Add(rawEntry);
        await db.SaveChangesAsync(ct);

        var entry = new Entry
        {
            RawEntryId = rawEntry.Id,
            UserId = userId,
            Type = type,
            Title = Truncate(rawText, 40),
            Status = "pending",
            CreatedAt = DateTime.Now,
        };
        db.Entries.Add(entry);
        await db.SaveChangesAsync(ct);
        return entry;
    }

    private static void ApplyResult(Entry entry, ParseResult result)
    {
        entry.Type = EntryType.All.Contains(result.Type) ? result.Type : EntryType.Note;
        entry.Title = string.IsNullOrWhiteSpace(result.Title) ? Truncate(entry.RawEntry.RawText, 40) : result.Title.Trim();
        entry.Amount = entry.Type is EntryType.Income or EntryType.Expense ? result.Amount : null;
        entry.Category = result.Category;
        entry.OccurredAt = TimeFormat.TryParseOccurredAt(result.OccurredAt);
        entry.Summary = string.IsNullOrWhiteSpace(result.Summary) ? null : result.Summary.Trim();
        entry.Status = "confirmed";
    }

    private static string MergeText(string rawText, List<QaAnswerDto> answers) =>
        answers.Count == 0
            ? rawText
            : rawText + "，补充回答：" + string.Join("；", answers.Select(a => a.Answer));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "…";

    private static EntryDto ToDto(Entry e, string rawText) => new(
        e.Id,
        e.Type,
        e.Title,
        e.Amount,
        e.Category,
        e.OccurredAt is null ? null : TimeFormat.DateTimeFlex(e.OccurredAt.Value),
        rawText,
        e.Summary,
        e.Status,
        TimeFormat.DateTimeFlex(e.CreatedAt));
}