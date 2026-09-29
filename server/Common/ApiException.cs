namespace Huamishu.Api.Common;

/// <summary>业务异常：携带 HTTP 状态码，由全局中间件统一渲染为 { "message": ... }。</summary>
public sealed class ApiException(int statusCode, string message) : Exception(message)
{
    public int StatusCode { get; } = statusCode;

    public static ApiException BadRequest(string message) => new(400, message);
    public static ApiException NotFound(string message) => new(404, message);
    public static ApiException Conflict(string message) => new(409, message);
    public static ApiException Unauthorized(string message) => new(401, message);
    public static ApiException TooManyRequests(string message) => new(429, message);
}