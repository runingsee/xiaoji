# 晓记 · V1 开发设计文档

> 项目：晓记（XiaoJi）｜ V1 完整方案
> 纳入四条产品想法：原始/分析分离、多条拆分、用户标签、统计优化
> 后端：ASP.NET Core 10 + MySQL 8 + EF Core 9
> 前端：Vue 3 + Vite + Vant 4
> AI：DeepSeek API（deepseek-chat）

---

## 一、项目全景

### 1.1 产品定位

口述 → 原始记录 → AI 分析 → 标签 → 账本/日程/知识库 → 对话查询。

用户像跟秘书说话一样把事件口述出来，系统自动拆分、分析、分类、入库，支持用户打标签，随时可查可问。

### 1.2 技术栈

| 层级 | 技术选型 | 说明 |
|------|---------|------|
| 前端框架 | Vue 3 + Vite + Vant 4 | 移动端 H5 |
| 前端语言 | TypeScript | 严格类型 |
| 后端框架 | ASP.NET Core 10 (.NET LTS) | 强类型 C# |
| 数据访问 | EF Core 9 + Pomelo.EntityFrameworkCore.MySql | .NET 标准 MySQL 驱动 |
| 数据库 | MySQL 8 | 已有服务 |
| AI 服务 | DeepSeek API (deepseek-chat) | OpenAI 兼容 |
| 认证 | JWT (JwtBearer) + BCrypt | 手机号+密码 |
| 文本拆分 | 服务端 TextSplitter | 规则拆分，不依赖 AI |
| 部署 | Nginx(HTTPS) → Kestrel | 国内轻量服务器 |

### 1.3 四条想法在架构中的位置

```
用户输入 ──→ TextSplitter.split() ──→ [segment1, segment2, ...]
    │                                      │
    │                              逐条独立处理
    │                                      │
    ▼                                      ▼
┌────────────┐     ┌──────────────────┐
│ raw_entries │     │ TextSplitter     │
│ (原始记录)   │     │ (拆分器)         │
│ 永不修改     │     │                  │
└─────┬───────┘     └────────┬─────────┘
      │ 1:N                  │ 逐条解析
      │                      ▼
      │           ┌──────────────────┐
      │           │ DeepSeekClient    │
      │           │ (AI 分析)         │
      │           └────────┬─────────┘
      │                    │
      │                    ▼
┌─────┴────────┐   ┌──────────────┐
│  entries     │   │ tags         │
│ (分析结果)    │   │ (标签表)      │
│ 可反复更新    │   └──────┬───────┘
│ user_id FK   │          │ N:M
└─────┬────────┘    ┌─────┴─────────┐
      │             │ entry_tags    │
      │             │ (记录-标签)   │
      │             └───────────────┘
      │
      └──→ 账本/日程/知识库统计（零 AI 调用）
```

### 1.4 核心设计原则

1. **原始可追溯**：`raw_entries` 永不修改，永久保留原始口述
2. **分析可重跑**：`entries` 可反复更新，支持换模型/调 prompt 重解析
3. **AI 只在录入时调用**：统计计算全部走 SQL，零 AI 调用
4. **标签用户隔离**：每个用户独立标签体系
5. **多条拆分前置**：服务端规则拆分，每条独立分析
6. **前后端同源**：一个 ASP.NET Core 服务，Nginx 只做 TLS 终结
7. **时间统一**：全站 Asia/Shanghai 墙钟时间

### 1.5 完整目录结构

```
huamishu/
├─ dev-design/                  # 本文件夹，开发设计文档
├─ server/                      # ASP.NET Core 后端
│  ├─ Huamishu.Api.csproj
│  ├─ Program.cs
│  ├─ appsettings.json
│  ├─ Data/
│  │  ├─ AppDbContext.cs
│  │  ├─ Entities/
│  │  │  ├─ User.cs
│  │  │  ├─ RawEntry.cs          # 新增
│  │  │  ├─ Entry.cs              # 修改：新增 raw_entry_id
│  │  │  ├─ ConfirmQa.cs
│  │  │  ├─ Tag.cs                # 新增
│  │  │  └─ EntryTag.cs           # 新增
│  │  ├─ schema.sql               # 更新
│  │  └─ TextSplitter.cs          # 新增：文本拆分器
│  ├─ Controllers/
│  │  ├─ AuthController.cs
│  │  ├─ EntriesController.cs     # 修改：parse 支持批量
│  │  ├─ TagsController.cs         # 新增
│  │  └─ AskController.cs
│  ├─ AI/
│  │  ├─ DeepSeekClient.cs
│  │  ├─ ParseModels.cs
│  │  └─ Prompts.cs
│  ├─ Services/
│  │  ├─ AuthService.cs
│  │  ├─ EntryService.cs          # 重写
│  │  ├─ TagService.cs            # 新增
│  │  └─ AskService.cs
│  ├─ Contracts/
│  │  ├─ AuthDtos.cs
│  │  ├─ EntryDtos.cs
│  │  └─ TagDtos.cs              # 新增
│  ├─ Common/
│  │  ├─ ApiException.cs
│  │  ├─ TimeFormat.cs
│  │  └─ ExceptionHandlingMiddleware.cs
│  └─ README.md
├─ web/                         # Vue3 + Vite + Vant 前端
│  ├─ src/
│  │  ├─ main.ts
│  │  ├─ App.vue
│  │  ├─ router/
│  │  ├─ api/
│  │  │  ├─ http.ts
│  │  │  └─ index.ts
│  │  └─ views/
│  │      ├─ LoginView.vue
│  │      ├─ InputView.vue           # 修改：批量结果展示
│  │      ├─ LedgerView.vue          # 修改：标签筛选
│  │      ├─ ScheduleView.vue
│  │      ├─ KnowledgeView.vue       # 修改：标签过滤
│  │      ├─ AskView.vue
│  │      └─ TagsView.vue            # 新增
│  └─ dist/
├─ deploy/
│  ├─ nginx.conf
│  ├─ backup.sh
│  ├─ xiaoji.service
│  └─ install.md
├─ 技术方案.md
├─ 项目说明.md
├─ 品牌方案.md
└─ myidea/myidea-analysis.md
```

---

## 二、文档索引

| 编号 | 文档 | 内容 |
|------|------|------|
| 00 | overview.md | 总览、架构、四条想法、目录结构、设计原则 |
| 01 | data-model.md | 6 张表 DDL、实体关系、索引、EF Core 配置 |
| 02 | api-spec.md | 17 个 API 的完整规范 |
| 03 | frontend-design.md | 7 个页面的 UI 布局、交互、标签管理 |
| 04 | ai-design.md | 文本拆分、DeepSeek 接入、解析链路、追问、RAG-lite |
| 05 | security-design.md | 认证、授权、数据隔离、限流、标签安全 |
| 06 | deployment.md | 服务器配置、Nginx、systemd、备份、迁移 |
| 07 | development-plan.md | Sprint 划分、任务拆分、里程碑 |
| 08 | code-conventions.md | C#/TypeScript 规范、命名、Git 规范 |
