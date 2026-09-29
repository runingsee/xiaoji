namespace Huamishu.Api.Contracts;

public sealed record RegisterRequest(string Phone, string Password, string? Nickname);
public sealed record LoginRequest(string Phone, string Password);

/// <summary>用户信息（不含任何敏感字段，成功响应统一携带 token）。</summary>
public sealed record UserDto(
    long Id,
    string Phone,
    string? Nickname,
    string Plan,
    DateTime? PlanExpires,
    string CreatedAt);

public sealed record AuthResponse(string Token, UserDto User);

public sealed record ApiError(string Message);