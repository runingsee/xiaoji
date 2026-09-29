using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Huamishu.Api.Common;

public static class ClaimsPrincipalExtensions
{
    public static long GetUserId(this ClaimsPrincipal principal)
    {
        var raw = principal.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
                  ?? throw ApiException.Unauthorized("登录已失效，请重新登录");
        return long.Parse(raw);
    }
}