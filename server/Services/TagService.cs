using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Data;
using Huamishu.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Huamishu.Api.Services;

/// <summary>标签管理：创建、列表、为记录挂标签。</summary>
public sealed class TagService(AppDbContext db)
{
    /// <summary>创建标签（同名去重，按用户隔离）。</summary>
    public async Task<TagDto> CreateAsync(long userId, string name, CancellationToken ct)
    {
        name = name.Trim();
        if (string.IsNullOrWhiteSpace(name))
            throw ApiException.BadRequest("标签名不能为空");
        if (name.Length > 50)
            throw ApiException.BadRequest("标签名最长 50 字符");

        var existing = await db.Tags.FirstOrDefaultAsync(
            t => t.UserId == userId && t.Name == name, ct);
        if (existing is not null)
            return ToDto(existing);

        var tag = new Tag { UserId = userId, Name = name, CreatedAt = DateTime.Now };
        db.Tags.Add(tag);
        await db.SaveChangesAsync(ct);
        return ToDto(tag);
    }

    /// <summary>获取用户全部标签。</summary>
    public async Task<List<TagDto>> ListAsync(long userId, CancellationToken ct)
    {
        return await db.Tags
            .Where(t => t.UserId == userId)
            .OrderByDescending(t => t.CreatedAt)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
    }

    /// <summary>删除标签（同时解除与记录的关联）。</summary>
    public async Task<bool> DeleteAsync(long userId, long tagId, CancellationToken ct)
    {
        var tag = await db.Tags.FirstOrDefaultAsync(
            t => t.Id == tagId && t.UserId == userId, ct);
        if (tag is null) return false;
        db.Tags.Remove(tag);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>为记录添加标签。</summary>
    public async Task AddTagsToEntryAsync(long userId, long entryId, List<long> tagIds, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(
            e => e.Id == entryId && e.UserId == userId, ct)
            ?? throw ApiException.NotFound("记录不存在");

        foreach (var tagId in tagIds.Distinct())
        {
            var tag = await db.Tags.FirstOrDefaultAsync(
                t => t.Id == tagId && t.UserId == userId, ct);
            if (tag is null) continue;

            var exists = await db.EntryTags.AnyAsync(
                et => et.EntryId == entryId && et.TagId == tagId, ct);
            if (!exists)
                db.EntryTags.Add(new EntryTag { EntryId = entryId, TagId = tagId });
        }
        await db.SaveChangesAsync(ct);
    }

    /// <summary>获取记录关联的标签。</summary>
    public async Task<List<TagDto>> GetEntryTagsAsync(long userId, long entryId, CancellationToken ct)
    {
        var tags = await db.EntryTags
            .Where(et => et.EntryId == entryId)
            .Join(db.Tags, et => et.TagId, t => t.Id, (et, t) => t)
            .Where(t => t.UserId == userId)
            .Select(t => ToDto(t))
            .ToListAsync(ct);
        return tags;
    }

    /// <summary>移除记录的某个标签。</summary>
    public async Task<bool> RemoveTagFromEntryAsync(long userId, long entryId, long tagId, CancellationToken ct)
    {
        var entry = await db.Entries.FirstOrDefaultAsync(
            e => e.Id == entryId && e.UserId == userId, ct);
        if (entry is null) return false;

        var link = await db.EntryTags.FirstOrDefaultAsync(
            et => et.EntryId == entryId && et.TagId == tagId, ct);
        if (link is null) return false;

        db.EntryTags.Remove(link);
        await db.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>按标签统计记录数与金额（income/expense），可按月份过滤。</summary>
    public async Task<List<TagStatsDto>> StatsAsync(long userId, string? month, CancellationToken ct)
    {
        var m = DateTime.TryParseExact(month, "yyyy-MM",
                System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var parsed)
            ? parsed
            : new DateTime(DateTime.Now.Year, DateTime.Now.Month, 1);
        var next = m.AddMonths(1);

        return await (
            from et in db.EntryTags
            join t in db.Tags on et.TagId equals t.Id
            join e in db.Entries on et.EntryId equals e.Id
            where t.UserId == userId
                  && e.UserId == userId
                  && e.OccurredAt >= m && e.OccurredAt < next
                  && (e.Type == EntryType.Income || e.Type == EntryType.Expense)
            group new { t.Name, e.Type, e.Amount } by new { t.Name, e.Type } into g
            select new TagStatsDto(g.Key.Name, g.Count(), g.Sum(x => x.Amount ?? 0), g.Key.Type)
        ).ToListAsync(ct);
    }

    private static TagDto ToDto(Tag t) => new(t.Id, t.Name, TimeFormat.DateTimeFlex(t.CreatedAt));
}
