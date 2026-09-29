# 晓记 · 开发里程碑与计划

> 基于 V1 完整方案（含四条产品想法）
> 目标：实现「口述 → 拆分 → 原始记录 → AI 分析 → 标签 → 入库 → 统计」完整链路

---

## 一、现有代码状态

| 模块 | 状态 | 文件 | 说明 |
|------|------|------|------|
| 后端骨架 | ✅ 已完成 | Program.cs, Controllers, Services | |
| AI 客户端 | ✅ 已完成 | DeepSeekClient.cs, ParseModels.cs, Prompts.cs | |
| 前端骨架 | ✅ 已完成 | 6 个页面 + API 封装 | |
| 数据库脚本 | ✅ 需更新 | schema.sql | 需新增 3 张表 |
| TextSplitter | ❌ 未实现 | 新增 | |
| TagService | ❌ 未实现 | 新增 | |
| 标签前端 | ❌ 未实现 | TagsView.vue | |
| 批量解析 | ❌ 需重写 | EntryService.cs | |

---

## 二、Sprint 划分

### Sprint 1：核心链路 + 数据模型（预计 5-7 天）

**目标**：搭建新数据模型，实现文本拆分 → 原始记录 → AI 分析 → 入库的完整链路

| 任务 | 文件 | 说明 |
|------|------|------|
| T1: 数据模型迁移 | `Data/Entities/`, `Data/schema.sql` | 新增 RawEntry, Tag, EntryTag 实体；更新 Entry 实体 |
| T2: EF Core 上下文更新 | `Data/AppDbContext.cs` | 添加新 DbSet 和关系配置 |
| T3: TextSplitter 实现 | `Data/TextSplitter.cs` | 规则拆分逻辑 |
| T4: EntryService 重写 | `Services/EntryService.cs` | 支持批量拆分，逐条分析 |
| T5: BatchParseResponse | `Contracts/EntryDtos.cs` | 新增批量响应模型 |
| T6: RawEntryRepository | `Data/` | 原始记录操作 |
| T7: AI 解析联调 | `AI/DeepSeekClient.cs` | 验证拆分后每条正确解析 |
| T8: 标签实体 + EF | `Data/Entities/Tag.cs`, `EntryTag.cs` | |
| T9: TagService | `Services/TagService.cs` | 标签 CRUD |
| T10: TagsController | `Controllers/TagsController.cs` | 标签管理 API |

**验收标准**：
- [ ] 输入多条文本能正确拆分
- [ ] 每条拆分结果各自创建 raw_entries 和 entries
- [ ] 追问流程在单条上正常工作
- [ ] 标签 CRUD API 正常
- [ ] 数据库 6 张表全部创建成功
- [ ] `dotnet build` 编译通过

---

### Sprint 2：前端批量 + 标签（预计 4-5 天）

**目标**：前端批量结果显示、标签管理、标签筛选

| 任务 | 文件 | 说明 |
|------|------|------|
| T11: InputView 改造 | `web/src/views/InputView.vue` | 批量结果显示 |
| T12: API 类型更新 | `web/src/api/index.ts` | BatchParseResponse 类型 |
| T13: TagsView 开发 | `web/src/views/TagsView.vue` | 标签管理页 |
| T14: LedgerView 标签筛选 | `web/src/views/LedgerView.vue` | 标签过滤 |
| T15: KnowledgeView 标签过滤 | `web/src/views/KnowledgeView.vue` | 标签过滤 |
| T16: 标签添加 UI | `web/src/views/` 各记录页 | 添加标签按钮 |
| T17: 路由更新 | `web/src/router/index.ts` | 新增 /tags 路由 |

**验收标准**：
- [ ] 口述后能显示批量处理结果
- [ ] 每条结果独立显示状态（已入账/追问中/挂起）
- [ ] 标签管理页能增删标签
- [ ] 账本能按标签筛选
- [ ] 知识库能按标签过滤
- [ ] 401 自动跳转登录

---

### Sprint 3：安全加固 + 部署（预计 2-3 天）

**目标**：安全加固、测试、部署上线

| 任务 | 文件 | 说明 |
|------|------|------|
| T18: 安全加固 | `Program.cs`, `deploy/nginx.conf` | 限流、JWT、HTTPS |
| T19: 配置管理 | `appsettings.json`, `.gitignore` | Secret 分离 |
| T20: 部署验证 | `deploy/` | systemd、Nginx、备份 |
| T21: 端到端测试 | 全部 | 完整流程走通 |
| T22: Bug 修复 | 全部 | 测试中发现的问题 |
| T23: schema.sql 最终确认 | `Data/schema.sql` | 含所有 6 张表 |

**验收标准**：
- [ ] 401 未授权访问被拒绝
- [ ] 限流生效（10次/10秒）
- [ ] HTTPS 可访问
- [ ] 备份脚本正常运行
- [ ] 服务崩溃后自动重启
- [ ] 完整流程无 Bug

---

## 三、每日开发节奏

| 时间 | 活动 |
|------|------|
| 上午 | 编码实现（核心功能） |
| 下午 | 联调 + 测试 + 修复 |
| 睡前 | 提交 Git、记录进度 |

---

## 四、代码质量检查清单

每个 PR 前确认：

- [ ] `dotnet build --no-restore` 编译通过
- [ ] `lsp_diagnostics` 无错误
- [ ] 无 `as any`、`@ts-ignore` 类型压制
- [ ] 无空 catch 块
- [ ] 所有查询带 `user_id` 过滤
- [ ] TextSplitter 单元测试覆盖
- [ ] 标签数据隔离验证通过
- [ ] API 路径命名一致
- [ ] 前后端接口匹配

---

## 五、技术债清单

| 项目 | 说明 | 预计解决版本 |
|------|------|-------------|
| EF Core 迁移 | 当前用手工 SQL | V2 再引入 Migrations |
| 前端状态管理 | V1 无状态库 | V2 如需复杂状态引入 Pinia |
| 日志系统 | 当前仅文件日志 | V2 引入 Serilog |
| 监控告警 | 当前无专业监控 | V2 引入 Prometheus + Grafana |
| 向量数据库 | 当前 RAG-lite | V2 如需语义搜索 |
| 语音录入 | V1 仅文字 | V2 |
| 图片 OCR | V1 无 | V3 |
| 支付系统 | V1 无 | V2 |
| 导出 Excel | V1 无 | V2 |
| 短信验证码 | V1 无 | V2 |

---

## 六、关键决策点

### 6.1 TextSplitter 分隔符策略

| 信号 | 行动 |
|------|------|
| 用户反馈拆分不准确 | 调整分隔符优先级或阈值 |
| 拆分比例 > 80% | 说明用户习惯一次性输入多条，规则合理 |
| 拆分后每条太短 | 降低 MinSegmentLength |

### 6.2 标签策略

| 信号 | 行动 |
|------|------|
| 标签使用率低 | V2 再优化标签 UI |
| 标签命名混乱 | 引入标签建议/自动补全 |
| 标签数量过多 | V2 引入标签分组 |

### 6.3 原始/分析分离策略

| 信号 | 行动 |
|------|------|
| 重解析需求高频 | 分离架构完全正确 |
| 性能问题 | 考虑合并查询优化 |

---

## 七、风险管理

| 风险 | 概率 | 影响 | 应对 |
|------|------|------|------|
| DeepSeek API 不可用 | 中 | 高 | 降级策略（pending + 重试） |
| 拆分逻辑不准确 | 中 | 中 | TextSplitter 规则明确，可快速调整 |
| 标签管理复杂 | 低 | 低 | 独立页面，不影响核心链路 |
| 原始记录表数据量大 | 低 | 中 | raw_entries 只存文本，体积小 |
| 数据库迁移问题 | 中 | 中 | V1 直接用新 schema，不迁移 |
| 前端批量展示复杂 | 中 | 中 | 每条独立展示，逻辑清晰 |
| 开发时间超预期 | 中 | 中 | Sprint 1 优先保证核心链路 |

---

## 八、开发优先级

### 第一优先级（必须完成）
1. TextSplitter 拆分逻辑
2. raw_entries + entries 分离存储
3. 逐条 AI 分析 + 批量响应
4. 追问流程适配拆分后的每条记录

### 第二优先级（应当完成）
1. 标签 CRUD API
2. 前端标签管理页
3. 账本/知识库标签筛选

### 第三优先级（尽量完成）
1. 标签统计 API
2. 前端标签添加 UI
3. 完整部署上线

---

## 九、完整开发流程

```
Sprint 1（第1周）:
  数据模型 → TextSplitter → EntryService 重写 → TagService → TagsController
    │
    ▼
Sprint 2（第2周）:
  前端批量展示 → TagsView → 标签筛选 → 标签添加 UI
    │
    ▼
Sprint 3（第3周）:
  安全加固 → 部署 → 测试 → 修复 → 上线
```

---

## 十、里程碑验收

### 里程碑 1：核心链路跑通（预计第 1 周末）

- [ ] 输入多条文本能正确拆分
- [ ] 每条独立创建 raw_entries + entries
- [ ] AI 解析每条准确
- [ ] 追问流程正常
- [ ] AI 降级正常
- [ ] `dotnet build` 通过

### 里程碑 2：前端功能完整（预计第 2 周末）

- [ ] 批量结果显示
- [ ] 标签管理页可用
- [ ] 账本/知识库标签筛选
- [ ] 所有页面正常
- [ ] 401 跳转登录

### 里程碑 3：部署上线（预计第 3 周末）

- [ ] 安全加固完成
- [ ] HTTPS 可访问
- [ ] 备份正常
- [ ] 服务自动重启
- [ ] 完整流程无 Bug
