using System.Text;
using System.Threading.RateLimiting;
using Huamishu.Api.AI;
using Huamishu.Api.Common;
using Huamishu.Api.Data;
using Huamishu.Api.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration;

// ---------------------------------------------------------------- 基础服务
builder.Services.AddControllers();
builder.Services.AddMemoryCache(); // 口述解析草稿（追问流程票据）暂存于此

// EF Core + MySQL（首查时连接，数据库下线不影响静态页面）
var connStr = config.GetConnectionString("MySql")
    ?? throw new InvalidOperationException("缺少 ConnectionStrings:MySql 配置");
builder.Services.AddDbContext<AppDbContext>(o =>
    o.UseMySql(connStr, new MySqlServerVersion(new Version(8, 0, 36))));

// ---------------------------------------------------------------- 业务服务
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EntryService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddScoped<AskService>();

builder.Services.AddHttpClient<DeepSeekClient>(c =>
{
    c.BaseAddress = new Uri(config["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com");
    c.Timeout = TimeSpan.FromSeconds(60);
});

// ---------------------------------------------------------------- JWT 认证
var jwtSecret = config["Jwt:Secret"]
    ?? throw new InvalidOperationException("缺少 Jwt:Secret 配置");
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(o =>
    {
        o.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = config["Jwt:Issuer"],
            ValidateAudience = true,
            ValidAudience = config["Jwt:Audience"],
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        };
    });
builder.Services.AddAuthorization();

// ---------------------------------------------------------------- 限流（登录/注册/解析：10 次/10 秒/IP）
builder.Services.AddRateLimiter(o =>
{
    o.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    o.AddPolicy("strict", ctx => RateLimitPartition.GetFixedWindowLimiter(
        ctx.Connection.RemoteIpAddress?.ToString() ?? "unknown",
        _ => new FixedWindowRateLimiterOptions
        {
            PermitLimit = 10,
            Window = TimeSpan.FromSeconds(10),
            QueueLimit = 0,
            AutoReplenishment = true,
        }));
});

// 开发期 CORS：仅本地 Vite 调试用（生产同源部署，不需要 CORS）
builder.Services.AddCors(o => o.AddPolicy("dev", p =>
    p.WithOrigins("http://localhost:5173").AllowAnyMethod().AllowAnyHeader()));

// ---------------------------------------------------------------- 管道
var app = builder.Build();

app.UseMiddleware<ExceptionHandlingMiddleware>();

if (app.Environment.IsDevelopment())
{
    app.UseCors("dev");
}

app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// 静态托管前端：发布时把 web/dist 复制为 wwwroot
var wwwroot = Path.Combine(app.Environment.ContentRootPath, "wwwroot");
if (Directory.Exists(Path.Combine(wwwroot, "index.html")))
{
    app.UseDefaultFiles();
    app.UseStaticFiles();
    app.MapFallbackToFile("index.html"); // SPA 路由回退
}

app.Run();