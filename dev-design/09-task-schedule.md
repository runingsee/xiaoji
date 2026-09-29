# 晓记 · 开发任务排期表

> 基于 V1 完整开发设计文档（含六条产品想法）
> 开发者：单人 | 工作日：周一至周五
> 假设：后端骨架已有，前端页面骨架已有

---

## 一、总览

| 阶段 | 时间 | 天数 | 目标 |
|------|------|------|------|
| 第 1 阶段 | 第 1-2 周 | 10 天 | 数据模型 + 核心链路 + 标签系统 |
| 第 2 阶段 | 第 3 周 | 5 天 | 前端批量展示 + 标签 UI + AI 搜索 |
| 第 3 阶段 | 第 4 周 | 5 天 | 安全加固 + 部署 + 测试 + 上线 |
| **合计** | **4 周** | **20 天** | |

---

## 二、详细任务排期

### 第 1 周：数据模型 + 核心链路

#### 第 1 天（周一）：数据模型 + EF Core

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 创建 `RawEntry.cs` 实体 | `server/Data/Entities/RawEntry.cs` | 原始记录实体 |
| 上午 | 创建 `Tag.cs` 实体 | `server/Data/Entities/Tag.cs` | 标签实体 |
| 上午 | 创建 `EntryTag.cs` 实体 | `server/Data/Entities/EntryTag.cs` | 记录-标签关联实体 |
| 下午 | 更新 `Entry.cs`（新增 raw_entry_id 等字段） | `server/Data/Entities/Entry.cs` | 更新后的实体 |
| 下午 | 更新 `AppDbContext.cs`（添加新 DbSet + 关系配置） | `server/Data/AppDbContext.cs` | EF Core 上下文 |
| 晚间 | 更新 `schema.sql`（6 张表完整脚本） | `server/Data/schema.sql` | 建表脚本 |
| 晚间 | `dotnet build` 验证编译 | — | 编译通过 |

**验收**：`dotnet build` 无错误，6 张表 SQL 完整

---

#### 第 2 天（周二）：TextSplitter + 拆分逻辑

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 实现 `TextSplitter.Split()` 方法 | `server/Data/TextSplitter.cs` | 规则拆分器 |
| 上午 | TextSplitter 单元测试（正常/边界/异常） | 测试文件 | 单元测试通过 |
| 下午 | 更新 `EntryDtos.cs`（新增 BatchParseResult、BatchParseResponse） | `server/Contracts/EntryDtos.cs` | 批量响应模型 |
| 下午 | 更新 `ParseModels.cs`（如需调整） | `server/AI/ParseModels.cs` | |
| 晚间 | `dotnet build` 验证 | — | 编译通过 |

**验收**：TextSplitter 能正确拆分各种输入，单元测试覆盖

---

#### 第 3 天（周三）：EntryService 重写（核心）

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 重写 `EntryService.ParseAsync()`：拆分 → 逐条分析 → 批量返回 | `server/Services/EntryService.cs` | 批量解析方法 |
| 上午 | 实现 `InsertRawEntryAsync()`：每条创建 raw_entries | `server/Services/EntryService.cs` | 原始记录入库 |
| 下午 | 实现 `InsertConfirmedEntryAsync()`：AI 分析结果入库 | `server/Services/EntryService.cs` | 分析结果入库 |
| 下午 | 实现 `InsertStubEntryAsync()`：AI 不可用降级 | `server/Services/EntryService.cs` | 降级逻辑 |
| 晚间 | 追问流程适配（每条独立草稿票） | `server/Services/EntryService.cs` | 追问逻辑 |
| 晚间 | `dotnet build` 验证 | — | 编译通过 |

**验收**：`dotnet build` 通过，逻辑正确（需调试验证）

---

#### 第 4 天（周四）：标签系统后端

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 实现 `TagService`（CRUD） | `server/Services/TagService.cs` | 标签服务 |
| 下午 | 创建 `TagsController` | `server/Controllers/TagsController.cs` | 标签 API |
| 下午 | 实现"给记录添加标签"逻辑 | `server/Services/EntryService.cs` 或 `TagService.cs` | 标签关联 |
| 下午 | 更新 `EntriesController`（添加 tagId 查询参数支持） | `server/Controllers/EntriesController.cs` | 标签过滤 |
| 晚间 | `dotnet build` 验证 | — | 编译通过 |

**验收**：标签 CRUD API 可用，列表支持 tagId 过滤

---

#### 第 5 天（周五）：AI 搜索 + 联调联试

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 实现 `POST /api/entries/search` 接口 | `Controllers/EntriesController.cs` + AI Prompt | AI 搜索 |
| 上午 | 编写 SearchIntentPrompt | `server/AI/Prompts.cs` | 意图解析 Prompt |
| 下午 | 全部 API 联调：解析 → 追问 → 确认 → 标签 → 搜索 | 全部后端文件 | 联调通过 |
| 下午 | `dotnet test`（如有测试） / 手动测试 API | Postman / curl | API 验证 |
| 晚间 | 修复 Bug | — | 稳定 |

**验收**：所有后端 API 可用，AI 搜索返回结构化结果

---

### 第 2 周：前端 + AI 搜索 UI

#### 第 6 天（周一）：前端批量结果显示

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 更新 `api/index.ts`（BatchParseResponse 类型 + 标签 API） | `web/src/api/index.ts` | 类型定义 |
| 上午 | 更新 `http.ts`（新增标签 API 方法） | `web/src/api/http.ts` | |
| 下午 | 改造 `InputView.vue`：批量结果显示 | `web/src/views/InputView.vue` | 每条独立状态 |
| 下午 | 追问弹层适配批量场景 | `web/src/views/InputView.vue` | |
| 晚间 | `npm run build` 验证前端编译 | — | 编译通过 |

**验收**：口述后能显示批量处理结果，每条独立状态

---

#### 第 7 天（周二）：标签管理页 + 路由

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 创建 `TagsView.vue`（标签增删改） | `web/src/views/TagsView.vue` | 标签管理页 |
| 下午 | 更新 `router/index.ts`（新增 /tags 路由） | `web/src/router/index.ts` | 路由配置 |
| 下午 | 更新 `App.vue`（如需 Tabbar 调整） | `web/src/App.vue` | |
| 晚间 | `npm run build` 验证 | — | 编译通过 |

**验收**：标签管理页可用，路由可达

---

#### 第 8 天（周三）：账本/知识库标签筛选

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | `LedgerView.vue`：添加标签筛选 UI | `web/src/views/LedgerView.vue` | 标签过滤 |
| 下午 | `KnowledgeView.vue`：添加标签过滤 UI | `web/src/views/KnowledgeView.vue` | 标签过滤 |
| 下午 | 日程页添加标签显示 | `web/src/views/ScheduleView.vue` | |
| 晚间 | `npm run build` 验证 | — | 编译通过 |

**验收**：账本/知识库可按标签筛选

---

#### 第 9 天（周四）：AI 搜索 UI + 集成

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 所有页面添加搜索框（精确/AI 模式切换） | 各 views | 搜索 UI |
| 下午 | `AskView.vue` 优化（如需调整） | `web/src/views/AskView.vue` | |
| 下午 | 前后端联调：搜索 API 对接 | — | 联调通过 |
| 晚间 | `npm run build` 验证 | — | 编译通过 |

**验收**：AI 搜索可用，精确搜索和 AI 搜索可切换

---

#### 第 10 天（周五）：全链路测试

| 时间 | 任务 | 产出 |
|------|------|------|
| 上午 | 完整流程测试：口述多条 → 拆分 → AI 分析 → 追问 → 入库 | 测试报告 |
| 上午 | 标签全流程测试：创建 → 添加 → 筛选 → 删除 | 测试报告 |
| 下午 | AI 搜索测试：模糊查询 | 测试报告 |
| 下午 | 边界测试：空输入、超长输入、并发、AI 不可用 | 测试报告 |
| 晚间 | Bug 修复 | 稳定版 |

**验收**：全链路无 Bug，核心功能稳定

---

### 第 3 周：安全加固 + 部署

#### 第 11 天（周一）：安全加固 + 配置管理

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 检查限流配置 | `server/Program.cs` | 确认限流生效 |
| 上午 | 检查 JWT 配置 | `server/Program.cs` | 确认认证 |
| 下午 | 更新 `.gitignore`（排除 appsettings.json） | `.gitignore` | Secret 安全 |
| 下午 | 创建 `appsettings.json` 模板（含占位符） | `server/appsettings.json` | 配置模板 |
| 晚间 | `dotnet build` 验证 | — | 编译通过 |

**验收**：无硬编码 Secret，限流和认证正常

---

#### 第 12 天（周二）：部署准备 + Nginx 配置

| 时间 | 任务 | 文件 | 产出 |
|------|------|------|------|
| 上午 | 更新 `deploy/nginx.conf`（含新 API 路径） | `deploy/nginx.conf` | Nginx 配置 |
| 下午 | 更新 `deploy/xiaoji.service`（确认配置路径） | `deploy/xiaoji.service` | systemd 配置 |
| 下午 | 更新 `deploy/backup.sh`（含新表备份） | `deploy/backup.sh` | 备份脚本 |
| 下午 | 本地 `dotnet publish` 验证 | — | 发布成功 |
| 晚间 | `web/dist` 构建验证 | — | 构建成功 |

**验收**：发布和构建均成功

---

#### 第 13 天（周三）：部署到服务器 + 验证

| 时间 | 任务 | 产出 |
|------|------|------|
| 上午 | 上传到服务器 | 部署完成 |
| 上午 | 执行 schema.sql（6 张表） | 数据库就绪 |
| 下午 | 配置 appsettings.json（真实 Secret） | 配置完成 |
| 下午 | systemd 启动 + 验证 | 服务运行 |
| 晚间 | Nginx 配置 + HTTPS 验证 | 可访问 |

**验收**：服务通过 HTTPS 可访问，所有 API 正常

---

#### 第 14 天（周四）：端到端测试 + 备份验证

| 时间 | 任务 | 产出 |
|------|------|------|
| 上午 | 完整端到端测试（从注册到查询） | 测试报告 |
| 下午 | 备份脚本执行 + 恢复验证 | 备份正常 |
| 下午 | 压力测试（限流验证） | 限流正常 |
| 晚间 | 崩溃恢复测试（systemd 自动重启） | 自动恢复 |

**验收**：所有环节正常，备份可恢复

---

#### 第 15 天（周五）：Bug 修复 + 文档完善

| 时间 | 任务 | 产出 |
|------|------|------|
| 全天 | 修复测试中发现的 Bug | 稳定版 |
| 全天 | 更新 `README.md`（项目说明） | 文档完善 |
| 全天 | 确认代码质量检查清单全部通过 | 代码审查 |
| 晚间 | Git 提交 | 代码提交 |

**验收**：所有检查项通过，代码可提交

---

## 三、每日开发节奏

| 时间 | 活动 |
|------|------|
| 9:00 - 12:00 | 编码实现（核心功能） |
| 13:30 - 17:30 | 联调 + 测试 + 修复 |
| 18:00 - 19:00 | `dotnet build` / `npm run build` 验证 |
| 20:00 - 21:00 | Git 提交 + 记录进度 |
| 21:00 | 休息 |

---

## 四、里程碑验收

### 里程碑 1：核心链路跑通（第 5 天周五）

- [ ] `dotnet build` 编译通过
- [ ] 输入多条文本能正确拆分
- [ ] 每条独立创建 raw_entries 和 entries
- [ ] 追问流程正常工作
- [ ] AI 降级正常（pending 挂起）
- [ ] 标签 CRUD API 可用
- [ ] AI 语义搜索返回结构化结果

### 里程碑 2：前端功能完整（第 10 天周五）

- [ ] 批量结果显示正常
- [ ] 标签管理页可用
- [ ] 账本/知识库标签筛选
- [ ] AI 搜索可用
- [ ] 所有页面正常
- [ ] 401 自动跳转登录
- [ ] `npm run build` 编译通过

### 里程碑 3：部署上线（第 15 天周五）

- [ ] 服务器部署成功
- [ ] HTTPS 可访问
- [ ] 备份脚本正常运行
- [ ] 服务崩溃后自动重启
- [ ] 完整端到端流程无 Bug
- [ ] 代码质量检查全部通过

---

## 五、任务依赖关系图

```
Day 1: 数据模型 ──────────────────┐
       ↓                          │
Day 2: TextSplitter ──────────────┤
       ↓                          │
Day 3: EntryService 重写 ←────────┘（依赖数据模型 + TextSplitter）
       ↓                          │
Day 4: 标签后端 ←──────────────────┘（依赖数据模型）
       ↓                          │
Day 5: AI 搜索 ←───────────────────┘（依赖 EntryService + Prompt）
       ↓
Day 6-7: 前端类型 + 批量展示 + 标签页（依赖后端 API）
       ↓
Day 8-9: 筛选 + 搜索 UI（依赖标签 + 搜索 API）
       ↓
Day 10: 全链路测试
       ↓
Day 11-13: 安全 + 部署
       ↓
Day 14-15: 测试 + 上线
```

**关键路径**：数据模型 → TextSplitter → EntryService → 标签后端 → AI 搜索 → 前端

---

## 六、风险与缓冲

| 风险 | 可能延期 | 缓冲方案 |
|------|---------|---------|
| EntryService 重写复杂 | +1 天 | 第 3 天加班或顺延到第 6 天上午 |
| TextSplitter 规则调整 | +0.5 天 | 规则简单，调整快 |
| 标签 UI 交互复杂 | +1 天 | 先基础功能，V2 再优化 |
| AI 搜索效果不理想 | +1 天 | 调整 Prompt 或降低置信度阈值 |
| 部署环境问题 | +1 天 | 本地验证先通过再部署 |

**总缓冲**：预留 3-5 天机动时间，不影响 4 周总体计划。

---

## 七、每日提交记录模板

```
Day 1: feat(data): add RawEntry, Tag, EntryTag entities + update schema.sql
Day 2: feat(splitter): implement TextSplitter with unit tests
Day 3: feat(entries): rewrite EntryService with batch parsing
Day 4: feat(tags): add TagService, TagsController, tag filtering
Day 5: feat(search): add AI semantic search endpoint
Day 6: feat(frontend): batch results display + API types
Day 7: feat(frontend): TagsView page + route
Day 8: feat(frontend): LedgerView + KnowledgeView tag filtering
Day 9: feat(frontend): AI search UI + mode switching
Day 10: fix: end-to-end testing and bug fixes
Day 11: fix(security): rate limiting, JWT, .gitignore
Day 12: feat(deploy): nginx, systemd, backup scripts
Day 13: deploy: server deployment and verification
Day 14: test: end-to-end testing and backup verification
Day 15: fix: bug fixes, code review, documentation
```
