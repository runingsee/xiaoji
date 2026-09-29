# 晓记 · 后端服务（Huamishu.Api）

ASP.NET Core 10 + EF Core(Pomelo/MySQL 8) + DeepSeek。提供口述解析、账本/日程/知识库检索、标签体系与对话咨询。

## 目录

| 路径 | 说明 |
| --- | --- |
| `Data/Entities/` | EF 实体（User / RawEntry / Entry / ConfirmQa / Tag / EntryTag） |
| `Data/AppDbContext.cs` | 上下文与关系/索引配置 |
| `Data/TextSplitter.cs` | 多条口述文本的规则拆分器 |
| `Data/schema.sql` | 6 张表建库脚本（部署时手工执行） |
| `AI/` | DeepSeek 客户端、Prompt 集中管理、解析/查询意图模型 |
| `Services/` | Auth / Entry / Tag / Ask 四个业务服务 |
| `Contracts/` | 请求响应 DTO |
| `Controllers/` | REST 接口 |
| `Common/` | 统一异常、中间件、时间格式、Claims 扩展 |
| `Huamishu.Api.Tests/` | xUnit 单元测试（当前覆盖 TextSplitter） |

## 本地运行

```bash
# 1) 建库（只需一次）
mysql -u root -p < Data/schema.sql

# 2) 配置：复制模板并填入真实值
cp appsettings.Example.json appsettings.json

# 3) 编译 + 跑测试
dotnet build
dotnet test

# 4) 启动（默认 http://localhost:5000）
dotnet run
```

`appsettings.json` 含数据库密码、JWT 密钥、DeepSeek Key，**已在 .gitignore 中排除，严禁提交**。

## 接口一览

| 方法 | 路径 | 限流 | 说明 |
| --- | --- | --- | --- |
| POST | `/api/auth/register` | strict | 注册（手机号 + 密码） |
| POST | `/api/auth/login` | strict | 登录，返回 JWT |
| GET | `/api/auth/me` | strict | 当前用户 |
| POST | `/api/entries/parse` | strict | 单条口述解析 |
| POST | `/api/entries/batch` | strict | 批量口述解析（先拆分再逐条） |
| POST | `/api/entries/confirm` | — | 追问确认 / 挂起条目补充后入库 |
| GET | `/api/entries` | — | 列表，支持 `type` / `month` / `q` / `tagId` |
| POST | `/api/entries/search` | strict | AI 语义搜索 |
| GET | `/api/entries/summary` | — | 月度收支汇总 |
| PATCH/DELETE | `/api/entries/{id}` | — | 错账修正 / 删除 |
| POST/GET/DELETE | `/api/tags` | strict | 标签 CRUD |
| POST/GET/DELETE | `/api/tags/entry/{id}/tags` | strict | 记录挂载 / 解绑标签 |
| GET | `/api/tags/stats` | strict | 按标签的收支统计（可按月） |
| POST | `/api/ask` | strict | 对话咨询（基于真实数据回答） |

除 `auth/register`、`auth/login` 外全部需要 `Authorization: Bearer <token>`。

## 安全策略

- **认证**：JWT Bearer，校验 issuer / audience / 签名 / 有效期，ClockSkew 1 分钟。
- **限流**：策略 `strict` 为固定窗口 10 次 / 10 秒 / IP，超限返回 429；Nginx 层对 `/api/auth/` 另有一道 `limit_req`。
- **越权防护**：所有查询强制带 `UserId` 条件；标签过滤会先校验标签归属，跨用户标签一律查不到。
- **CORS**：仅开发环境放行 `http://localhost:5173`，生产同源部署不需要 CORS。
- **降级**：DeepSeek 不可用时原文挂起为 `pending`，不丢数据，确认时重解析。

## 发布

```bash
dotnet publish Huamishu.Api.csproj -c Release -r linux-x64 --self-contained
```

前端 `npm run build` 产物复制进发布目录 `wwwroot/`，服务会自动做 SPA 回退。服务器部署步骤见仓库根 `deploy/install.md`。
