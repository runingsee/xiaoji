using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Huamishu.Api.Controllers;

[ApiController]
[Authorize]
[Route("api/ask")]
[EnableRateLimiting("strict")]
public sealed class AskController(AskService svc) : ControllerBase
{
    [HttpPost]
    public async Task<ActionResult<AskResponse>> Ask(
        [FromBody] AskRequest req, CancellationToken ct)
        => Ok(await svc.AskAsync(User.GetUserId(), req.Question ?? "", ct));
}