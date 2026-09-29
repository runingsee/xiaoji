using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Huamishu.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/entries")]
public sealed class EntriesController(EntryService svc) : ControllerBase
{
    private long Uid => User.GetUserId();

    /// <summary>口述解析：信息完整直接入库；缺信息发追问；AI 挂了挂起。</summary>
    [HttpPost("parse")]
    [EnableRateLimiting("strict")]
    public async Task<ActionResult<ParseResponse>> Parse(
        [FromBody] CreateEntryRequest req, CancellationToken ct)
        => Ok(await svc.ParseAsync(Uid, req.RawText ?? "", ct));

    /// <summary>批量解析：先拆分文本，再逐段 AI 解析，各自入库。</summary>
    [HttpPost("batch")]
    [EnableRateLimiting("strict")]
    public async Task<ActionResult<BatchParseResponse>> Batch(
        [FromBody] CreateEntryRequest req, CancellationToken ct)
        => Ok(await svc.BatchParseAsync(Uid, req.RawText ?? "", ct));

    /// <summary>确认入账：合并追问回答（或挂起条目的澄清文本）后入库。</summary>
    [HttpPost("confirm")]
    public async Task<ActionResult<ParseResponse>> Confirm(
        [FromBody] ConfirmEntryRequest req, CancellationToken ct)
        => Ok(await svc.ConfirmAsync(Uid, req, ct));

    /// <summary>列表：可按 type / month(yyyy-MM) / q / tagId 过滤。</summary>
    [HttpGet]
    public async Task<ActionResult<List<EntryDto>>> List(
        [FromQuery] string? type, [FromQuery] string? month, [FromQuery] string? q, [FromQuery] long? tagId, CancellationToken ct)
        => Ok(await svc.ListAsync(Uid, type, month, q, tagId, ct));

    /// <summary>AI 搜索：用自然语言查询，按意图过滤。</summary>
    [HttpPost("search")]
    [EnableRateLimiting("strict")]
    public async Task<ActionResult<List<EntryDto>>> Search(
        [FromBody] SearchRequest req, CancellationToken ct)
        => Ok(await svc.SearchAsync(Uid, req.Query ?? "", ct));

    /// <summary>月度收支汇总。</summary>
    [HttpGet("summary")]
    public async Task<ActionResult<MonthSummaryDto>> Summary(
        [FromQuery] string? month, CancellationToken ct)
        => Ok(await svc.SummaryAsync(Uid, month, ct));

    /// <summary>错账修正。</summary>
    [HttpPatch("{id:long}")]
    public async Task<ActionResult<EntryDto>> Update(
        long id, [FromBody] UpdateEntryRequest req, CancellationToken ct)
        => Ok(await svc.UpdateAsync(Uid, id, req, ct));

    /// <summary>删除。</summary>
    [HttpDelete("{id:long}")]
    public async Task<ActionResult> Delete(long id, CancellationToken ct)
        => await svc.DeleteAsync(Uid, id, ct) ? NoContent() : NotFound();
}