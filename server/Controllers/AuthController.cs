using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Huamishu.Api.Controllers;

[ApiController]
[Route("api/auth")]
[EnableRateLimiting("strict")]
public sealed class AuthController(AuthService auth) : ControllerBase
{
    [HttpPost("register")]
    public async Task<ActionResult<AuthResponse>> Register(
        [FromBody] RegisterRequest req, CancellationToken ct)
        => Ok(await auth.RegisterAsync(req.Phone, req.Password, req.Nickname, ct));

    [HttpPost("login")]
    public async Task<ActionResult<AuthResponse>> Login(
        [FromBody] LoginRequest req, CancellationToken ct)
        => Ok(await auth.LoginAsync(req.Phone, req.Password, ct));

    [Authorize]
    [HttpGet("me")]
    public async Task<ActionResult<UserDto>> Me(CancellationToken ct)
        => Ok(await auth.GetAsync(User.GetUserId(), ct));
}