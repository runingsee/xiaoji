# 晓记 · 安全设计

> 安全目标：保护用户数据、防止未授权访问、确保 API 安全
> 原则：纵深防御，最小权限，默认安全

---

## 一、认证安全

### 1.1 密码存储

| 项目 | 实现 | 说明 |
|------|------|------|
| 算法 | BCrypt (BCrypt.Net-Next v4.2.0) | 自适应哈希，自动加盐 |
| 工作因子 | 默认 12 | |
| 存储长度 | 60 字符 | `$2a$12$...` 格式 |
| 验证方式 | `BCrypt.Verify(password, hash)` | 常数时间比较 |

### 1.2 JWT 令牌

| 项目 | 值 |
|------|------|
| 算法 | HS256 |
| 有效期 | 30 天 |
| 签发者 | `xiaoji` |
| 接收者 | `xiaoji-web` |
| 时钟偏差 | 1 分钟 |

```csharp
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
```

---

## 二、授权安全

### 2.1 控制器级授权

| 控制器 | 授权要求 | 说明 |
|--------|---------|------|
| AuthController | ❌ 无需授权 | 注册/登录/用户信息 |
| EntriesController | ✅ `[Authorize]` | 所有记录操作需登录 |
| TagsController | ✅ `[Authorize]` | 标签管理需登录 |
| AskController | ✅ `[Authorize]` | 对话查询需登录 |

### 2.2 数据隔离（多租户）

所有查询强制带上 `user_id` 过滤：

```csharp
// raw_entries 查询
var rawEntries = await db.RawEntries.Where(e => e.UserId == userId).ToListAsync(ct);

// entries 查询
var entries = await db.Entries.Where(e => e.UserId == userId).AsQueryable();

// tags 查询
var tags = await db.Tags.Where(t => t.UserId == userId).ToListAsync(ct);

// 删除操作验证归属
var entry = await db.Entries.FirstOrDefaultAsync(
    e => e.Id == id && e.UserId == userId, ct);
```

**绝不**出现无 `user_id` 过滤的全表查询。

---

## 三、标签安全

### 3.1 标签隔离

| 项目 | 实现 | 说明 |
|------|------|------|
| 用户隔离 | `WHERE UserId = ?` | 用户只能访问自己的标签 |
| 标签创建 | 创建时绑定 userId | 不能指定其他用户 |
| 标签删除 | 验证 userId | 不能删除他人的标签 |
| 记录标签 | 验证记录归属 | 只能给属于自己的记录打标签 |

### 3.2 标签名校验

- 最大 50 字符
- 不允许 HTML 标签或脚本（前端 + 后端双重校验）
- 不允许空字符串

---

## 四、限流安全

### 4.1 限流策略

```csharp
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
```

### 4.2 限流范围

| 接口 | 限流 | 说明 |
|------|------|------|
| POST /api/auth/register | ✅ 10次/10秒/IP | 防刷注册 |
| POST /api/auth/login | ✅ 10次/10秒/IP | 防暴力破解 |
| POST /api/entries/parse | ✅ 10次/10秒/IP | 防 AI 接口滥用 |
| 其他接口 | ❌ | 用户已认证，正常操作 |

---

## 五、输入校验

### 5.1 校验清单

| 接口 | 校验项 | 校验方式 |
|------|--------|---------|
| register | phone 格式 | Regex `^1[3-9]\d{9}$` |
| register | password 长度 | `password.Length >= 6` |
| register | 手机号唯一性 | DB 查询 `AnyAsync` |
| entries/parse | rawText 非空 | `string.IsNullOrEmpty` 检查 |
| entries/parse | 拆分后条数 | `Split().Count <= 10` |
| entries/confirm | draftToken 有效 | IMemoryCache 查找 |
| tags/create | name 非空且合法 | 长度和字符校验 |
| tags/create | 同名校验 | `UNIQUE KEY uk_user_name` |
| entries/:id/tags | 记录归属 | user_id 校验 |
| ask | question 非空 | `string.IsNullOrWhiteSpace` |

### 5.2 校验原则

- **永远不要信任前端输入**
- 所有校验在后端进行
- 校验失败返回 400 + 明确的错误消息
- 错误消息不泄露内部信息

---

## 六、数据安全

### 6.1 原始记录保护

| 字段 | 保护方式 | 说明 |
|------|---------|------|
| raw_entries.raw_text | 只增不改 | 永不更新，保证原始性 |
| entries | 可更新 | 重解析时只改 entries |
| confirm_qas | 只增不改 | 追问记录不可修改 |

### 6.2 敏感字段保护

| 字段 | 保护方式 | 说明 |
|------|---------|------|
| password_hash | BCrypt 哈希 | 不可逆 |
| DeepSeek API Key | 配置文件 + .gitignore | 不进代码仓库 |
| JWT Secret | 配置文件 + .gitignore | 不进代码仓库 |
| 手机号 | 返回给前端 | 用户已登录 |

### 6.3 数据库安全

| 项目 | 实现 | 说明 |
|------|------|------|
| 数据库账号 | 专用低权限账号 `xiaoji` | 不使用 root |
| 权限 | SELECT/INSERT/UPDATE/DELETE | 不给 DROP/ALTER |
| 字符集 | utf8mb4 | 支持 emoji |
| 备份 | mysqldump 每日 | deploy/backup.sh |

### 6.4 API Key 管理

**`appsettings.json`**：
```json
{
  "ConnectionStrings": { "MySql": "..." },
  "Jwt": { "Secret": "...", "Issuer": "xiaoji", "Audience": "xiaoji-web" },
  "DeepSeek": { "ApiKey": "...", "BaseUrl": "https://api.deepseek.com", "Model": "deepseek-chat" }
}
```

**`.gitignore`** 排除 `appsettings.json`、`appsettings.Development.json`

---

## 七、传输安全

### 7.1 HTTPS 强制

生产环境全部通过 HTTPS 通信：
- Nginx 终止 TLS
- HSTS 头
- HTTP → HTTPS 301 重定向

```nginx
add_header Strict-Transport-Security "max-age=31536000" always;
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
```

### 7.2 安全响应头

```nginx
add_header X-Content-Type-Options "nosniff" always;
add_header X-Frame-Options "DENY" always;
add_header Content-Security-Policy "default-src 'self'" always;
```

---

## 八、日志与审计

### 8.1 日志级别

```json
{ "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } } }
```

### 8.2 关键日志

| 事件 | 日志级别 | 内容 |
|------|---------|------|
| AI 调用失败 | Warning | 截断的请求内容 |
| 解析降级 | Warning | 原始文本（截断） |
| 用户操作 | Information | 注册、登录、创建记录 |
| 标签操作 | Information | 创建/删除标签 |
| 限流触发 | Warning | IP 地址 |
| 异常 | Error | ExceptionHandlingMiddleware |

### 8.3 审计日志

- `raw_entries` 保留原始口述，永不修改
- `entries` 更新时 `updated_at` 自动记录
- `confirm_qas` 记录完整问答链
- `tags` + `entry_tags` 记录标签操作

---

## 九、红线与不可为

| 红线 | 说明 |
|------|------|
| ✅ 密码 BCrypt | 绝不存明文 |
| ✅ JWT + JwtBearer | Token 自动校验 |
| ✅ HTTPS 强制 | Nginx TLS 终止 |
| ✅ DeepSeek Key 只在后端 | 前端零暴露 |
| ✅ 登录/录入限流 | 10次/10秒/IP |
| ✅ MySQL 专用低权限账号 | 不用 root |
| ✅ raw_entries 永不可变 | 原始记录保护 |
| ✅ 标签用户隔离 | 用户只能访问自己的标签 |
| ✅ 所有查询带 user_id | 数据隔离 |
| ✅ 金额 DECIMAL 精确存储 | 无浮点问题 |
| ✅ 时间统一 Asia/Shanghai | 不涉时区换算 |
| ❌ 不提交 Secret | .gitignore 排除 |
| ❌ 不返回详细错误信息 | 错误消息用户友好 |
| ❌ 空 catch 块 | 必须处理异常 |
| ❌ 前端硬编码 API Key | 所有密钥在后端 |

---

## 十、安全测试清单

| 测试项 | 方法 | 预期结果 |
|--------|------|---------|
| 弱密码注册 | 传密码 "123" | 400 |
| 非法手机号 | 传 "12345" | 400 |
| 重复注册 | 同一手机号两次 | 409 |
| 错误密码登录 | 密码错误 | 401 |
| 无 Token 访问 | 不带 Authorization 访问 /api/entries | 401 |
| 伪造 Token | 修改 JWT payload | 401 |
| 越权访问 | 用户 A 访问用户 B 的记录 | 404 |
| 限流 | 10 秒内 11 次请求 | 第 11 次返回 429 |
| 标签越权 | 用户 A 给用户 B 的记录打标签 | 404 |
| 标签重复 | 同一用户创建同名标签 | 409 |
| SQL 注入 | 传特殊字符搜索 | 空结果，不报错 |
| 拆分注入 | 超长或超多段输入 | 返回错误或截断 |
