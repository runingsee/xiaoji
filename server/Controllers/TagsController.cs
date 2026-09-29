using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Huamishu.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/tags")]
[EnableRateLimiting("strict")]
public sealed class TagsController(TagService svc) : ControllerBase
{
    private long Uid => User.GetUserId();

    /// <summary>创建标签（同名去重）。</summary>
    [HttpPost]
    public async Task<ActionResult<TagDto>> Create(
        [FromBody] CreateTagRequest req, CancellationToken ct)
        => Ok(await svc.CreateAsync(Uid, req.Name, ct));

    /// <summary>获取用户全部标签。</summary>
    [HttpGet]
    public async Task<ActionResult<List<TagDto>>> List(CancellationToken ct)
        => Ok(await svc.ListAsync(Uid, ct));

    /// <summary>删除标签。</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
        => await svc.DeleteAsync(Uid, id, ct) ? NoContent() : NotFound();

    /// <summary>为记录添加标签。</summary>
    [HttpPost("entry/{entryId:long}/tags")]
    public async Task<ActionResult> AddToEntry(
        long entryId, [FromBody] AddTagToEntryRequest req, CancellationToken ct)
    {
        await svc.AddTagsToEntryAsync(Uid, entryId, req.TagIds, ct);
        return NoContent();
    }

    /// <summary>获取记录关联的标签。</summary>
    [HttpGet("entry/{entryId:long}/tags")]
    public async Task<ActionResult<List<TagDto>>> GetEntryTags(long entryId, CancellationToken ct)
        => Ok(await svc.GetEntryTagsAsync(Uid, entryId, ct));

    /// <summary>移除记录标签。</summary>
    [HttpDelete("entry/{entryId:long}/tags/{tagId:long}")]
    public async Task<ActionResult> RemoveTag(long entryId, long tagId, CancellationToken ct)
        => await svc.RemoveTagFromEntryAsync(Uid, entryId, tagId, ct) ? NoContent() : NotFound();

    /// <summary>按标签统计（收入/支出的记录数与金额，可按 month=yyyy-MM 过滤）。</summary>
    [HttpGet("stats")]
    public async Task<ActionResult<List<TagStatsDto>>> Stats([FromQuery] string? month, CancellationToken ct)
        => Ok(await svc.StatsAsync(Uid, month, ct));
}
