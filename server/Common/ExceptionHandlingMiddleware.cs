using Huamishu.Api.Contracts;
using System.Net;

namespace Huamishu.Api.Common;

/// <summary>把业务异常渲染为统一 JSON 错误响应；未知异常记日志并返回 500。</summary>
public sealed class ExceptionHandlingMiddleware(RequestDelegate next, ILogger<ExceptionHandlingMiddleware> log)
{
    public async Task InvokeAsync(HttpContext ctx)
    {
        try
        {
            await next(ctx);
        }
        catch (ApiException ex)
        {
            ctx.Response.StatusCode = ex.StatusCode;
            ctx.Response.ContentType = "application/json; charset=utf-8";
            await ctx.Response.WriteAsJsonAsync(new ApiError(ex.Message));
        }
        catch (Exception ex)
        {
            log.LogError(ex, "未处理异常: {Path}", ctx.Request.Path);
            if (!ctx.Response.HasStarted)
            {
                ctx.Response.StatusCode = (int)HttpStatusCode.InternalServerError;
                ctx.Response.ContentType = "application/json; charset=utf-8";
                await ctx.Response.WriteAsJsonAsync(new ApiError("服务器开小差了，请稍后再试"));
            }
        }
    }
}