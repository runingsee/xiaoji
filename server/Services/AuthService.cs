using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using System.Text.RegularExpressions;
using Huamishu.Api.Common;
using Huamishu.Api.Contracts;
using Huamishu.Api.Data;
using Huamishu.Api.Data.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

namespace Huamishu.Api.Services;

public sealed partial class AuthService(AppDbContext db, IConfiguration config)
{
    private static readonly TimeSpan TokenLifetime = TimeSpan.FromDays(30);

    [GeneratedRegex(@"^1[3-9]\d{9}$")]
    private static partial Regex PhoneRegex();

    public async Task<AuthResponse> RegisterAsync(string phone, string password, string? nickname, CancellationToken ct)
    {
        phone = (phone ?? "").Trim();
        if (!PhoneRegex().IsMatch(phone))
            throw ApiException.BadRequest("手机号格式不正确");

        if (string.IsNullOrEmpty(password) || password.Length < 6)
            throw ApiException.BadRequest("密码至少 6 位");

        if (await db.Users.AnyAsync(u => u.Phone == phone, ct))
            throw ApiException.Conflict("该手机号已注册，直接登录即可");

        var user = new User
        {
            Phone = phone,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
            Nickname = string.IsNullOrWhiteSpace(nickname) ? null : nickname.Trim(),
            CreatedAt = DateTime.Now,
        };
        db.Users.Add(user);
        await db.SaveChangesAsync(ct);

        return new AuthResponse(CreateToken(user), ToDto(user));
    }

    public async Task<AuthResponse> LoginAsync(string phone, string password, CancellationToken ct)
    {
        var user = await db.Users.FirstOrDefaultAsync(u => u.Phone == (phone ?? "").Trim(), ct);
        if (user is null || !BCrypt.Net.BCrypt.Verify(password ?? "", user.PasswordHash))
            throw ApiException.Unauthorized("手机号或密码错误");

        return new AuthResponse(CreateToken(user), ToDto(user));
    }

    public async Task<UserDto> GetAsync(long userId, CancellationToken ct)
    {
        var user = await db.Users.FindAsync([userId], ct);
        return user is null
            ? throw ApiException.NotFound("用户不存在")
            : ToDto(user);
    }

    private string CreateToken(User user)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(config["Jwt:Secret"]
            ?? throw new InvalidOperationException("缺少 Jwt:Secret 配置")));

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity([
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim("phone", user.Phone),
            ]),
            Expires = DateTime.UtcNow.Add(TokenLifetime),
            Issuer = config["Jwt:Issuer"],
            Audience = config["Jwt:Audience"],
            SigningCredentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256),
        };

        var handler = new JwtSecurityTokenHandler();
        return handler.WriteToken(handler.CreateToken(descriptor));
    }

    private static UserDto ToDto(User u) => new(
        u.Id,
        u.Phone,
        u.Nickname,
        u.Plan,
        u.PlanExpires,
        TimeFormat.DateTimeFlex(u.CreatedAt));
}