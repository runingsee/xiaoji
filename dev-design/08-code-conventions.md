# 晓记 · 代码规范与约定

> 适用于全栈（后端 C# + 前端 TypeScript/Vue）
> 原则：一致性 > 正确性，可维护性 > 聪明性

---

## 一、后端 C# 规范

### 1.1 命名规范

| 类别 | 规范 | 示例 |
|------|------|------|
| 类名 | PascalCase | `EntryService`、`RawEntry`、`TextSplitter` |
| 方法名 | PascalCase | `ParseAsync`、`Split`、`AddTagsToEntry` |
| 属性名 | PascalCase | `RawEntryId`、`UserId` |
| 参数名 | camelCase | `rawText`、`tagNames` |
| 局部变量 | camelCase | `segments`、`result` |
| 常量 | PascalCase | `MinSegmentLength`、`MaxSegments` |
| 数据库列名 | PascalCase（EF 默认） | `RawEntryId`、`CreatedAt` |
| 实体类名 | PascalCase 单数 | `RawEntry`、`Entry`、`Tag`、`EntryTag` |

### 1.2 文件组织

```
server/
├─ Controllers/     一个控制器一个文件
├─ Services/        一个服务一个文件
├─ AI/              AI 相关类
├─ Data/
│  ├─ Entities/     实体类（每个实体一个文件）
│  ├─ TextSplitter.cs  文本拆分器
│  └─ schema.sql    建表脚本
├─ Contracts/       DTO/请求响应模型
├─ Common/          通用工具/异常
└─ Middleware/      中间件
```

### 1.3 代码风格

#### 类结构顺序
```csharp
public sealed class EntryService(AppDbContext db, DeepSeekClient ai, IMemoryCache drafts)
{
    // 1. 静态字段/常量
    private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(10);
    private const int MinSegmentLength = 5;
    private const int MaxSegments = 10;

    // 2. 私有记录类型
    private sealed record DraftState(string RawText, ParseResult Result, List<QaAnswerDto> Answers);

    // 3. 公共方法（按功能分组）
    public async Task<BatchParseResponse> ParseAsync(...) { ... }
    public async Task<BatchParseResponse> ConfirmAsync(...) { ... }
    public async Task<List<EntryDto>> ListAsync(...) { ... }

    // 4. 私有方法
    private async Task<RawEntry> InsertRawEntryAsync(...) { ... }
    private async Task<Entry> InsertConfirmedEntryAsync(...) { ... }
    private static List<string> SplitSegments(string rawText) => TextSplitter.Split(rawText);
}
```

#### TextSplitter 实现
```csharp
public static class TextSplitter
{
    private const int MinSegmentLength = 5;
    private const int MaxSegments = 10;

    public static List<string> Split(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return [];

        var trimmed = rawText.Trim();

        // 优先按换行拆分
        var lines = trimmed.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
            return lines.Select(l => l.Trim())
                .Where(l => l.Length >= MinSegmentLength)
                .Take(MaxSegments).ToList();

        // 其次按中文分隔符拆分
        var parts = trimmed.Split(['；', '、', '。'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
            return parts.Select(p => p.Trim())
                .Where(p => p.Length >= MinSegmentLength)
                .Take(MaxSegments).ToList();

        return [trimmed];
    }
}
```

#### 异步模式
```csharp
// ✅ 正确
public async Task<BatchParseResponse> ParseAsync(long userId, string rawText, CancellationToken ct)
{
    var segments = TextSplitter.Split(rawText);
    var results = new List<BatchParseResult>();
    foreach (var segment in segments)
    {
        results.Add(await ParseSingleAsync(userId, segment, ct));
    }
    return new BatchParseResponse(results);
}

// ❌ 错误：async void
public async void Process() { ... }

// ❌ 错误：Task.Run 包装同步方法
await Task.Run(() => SomeSyncMethod());
```

#### 异常处理
```csharp
// ✅ 正确
throw ApiException.BadRequest("请先说点什么再记");
throw ApiException.NotFound("记录不存在");
throw ApiException.Conflict("该标签已存在");

// ❌ 错误
throw new Exception("出错了");
catch { }  // 空 catch
```

### 1.4 EF Core 使用规范

```csharp
// ✅ 正确：可翻译的 LINQ
var query = db.Entries.Where(e => e.UserId == userId).AsQueryable();
if (!string.IsNullOrWhiteSpace(type))
    query = query.Where(e => e.Type == type);

// ✅ 正确：JOIN 关联表（标签过滤）
var entries = await db.Entries
    .Where(e => e.UserId == userId)
    .Where(e => tagId == null || db.EntryTags.Any(et => et.EntryId == e.Id && et.TagId == tagId))
    .ToListAsync(ct);

// ❌ 错误：在 LINQ 中调用客户端方法
query = query.Where(e => e.Title.ToUpper() == "TEST");

// ❌ 错误：ToList() 后再过滤
var all = await db.Entries.ToListAsync();
var filtered = all.Where(e => e.Type == "expense");
```

### 1.5 依赖注入

```csharp
// ✅ 正确
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<EntryService>();
builder.Services.AddScoped<TagService>();
builder.Services.AddHttpClient<DeepSeekClient>(c => { ... });
builder.Services.AddMemoryCache();

// ❌ 错误：Singleton 注入有状态服务
builder.Services.AddSingleton<EntryService>();
```

---

## 二、前端 TypeScript 规范

### 2.1 命名规范

| 类别 | 规范 | 示例 |
|------|------|------|
| 接口名 | PascalCase | `EntryDto`、`BatchParseResponse`、`TagDto` |
| 类型别名 | PascalCase | `EntryType = 'expense' \| 'income' \| ...` |
| 函数/方法 | camelCase | `handleBatchResponse()`、`loadTags()` |
| 变量 | camelCase | `rawText`、`newTagName` |
| 常量 | PascalCase | `MinSegmentLength` |
| 文件名 | kebab-case | `entryApi.ts`、`tags-view.vue` |
| Vue 组件 | PascalCase + .vue | `InputView.vue`、`TagsView.vue` |
| API 方法 | camelCase | `entryApi.parse()`、`tagApi.list()` |

### 2.2 代码风格

#### TypeScript 严格模式
```typescript
// ✅ 正确：类型标注
const res: BatchParseResponse = await entryApi.parse(text);
const tags: TagDto[] = await tagApi.list();

// ✅ 正确：接口定义
export interface BatchParseResult {
  entry: EntryDto | null;
  needsConfirm: boolean;
  reason?: 'missing' | 'parse_failed' | null;
  questions?: MissingField[];
  draftToken?: string | null;
  hint?: string | null;
}

export interface BatchParseResponse {
  batchResults: BatchParseResult[];
  totalCount: number;
  hint: string | null;
}

// ❌ 错误：使用 any
const data: any = await api.call();

// ❌ 错误：@ts-ignore
// @ts-ignore
const result = someFunc();
```

#### Vue 3 Composition API
```vue
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { showSuccessToast, showToast } from 'vant'
import { entryApi, tagApi, type BatchParseResponse, type TagDto } from '../api'

const rawText = ref('')
const submitting = ref(false)
const newTagName = ref('')
const tags = ref<TagDto[]>([])

async function submit() {
  const text = rawText.value.trim()
  if (!text || submitting.value) return
  submitting.value = true
  try {
    const res: BatchParseResponse = await entryApi.parse(text)
    handleBatchResponse(res)
    await loadHistory()
  } finally {
    submitting.value = false
  }
}

async function handleBatchResponse(res: BatchParseResponse) {
  showToast(`已拆分 ${res.totalCount} 条记录`)
  for (const result of res.batchResults) {
    if (!result.needsConfirm) {
      showSuccessToast('已入账')
    } else if (result.reason === 'missing') {
      showConfirmPopup(result)
    }
  }
}

async function addTag() {
  const name = newTagName.value.trim()
  if (!name) return
  await tagApi.create(name)
  newTagName.value = ''
  await loadTags()
}

onMounted(() => { loadHistory(); loadTags() })
</script>
```

### 2.3 批量结果显示

```vue
<!-- 批量结果显示模板 -->
<template>
  <div class="batch-results">
    <van-cell-group title="处理结果">
      <van-cell v-for="(result, index) in batchResults" :key="index"
        :title="result.entry?.title"
        :label="formatAmount(result.entry)"
        :value="getStatusLabel(result)" />
    </van-cell-group>
  </div>
</template>

<script setup lang="ts">
const getStatusLabel = (result: BatchParseResult) => {
  if (!result.needsConfirm) return '✅ 已入账'
  if (result.reason === 'missing') return '⏳ 追问中'
  return '⚠️ 已挂起'
}
</script>
```

---

## 三、Git 规范

### 3.1 提交消息格式

```
<类型>(<范围>): <描述>

[可选的正文]
```

示例：
```
feat(entries): 添加文本拆分和批量解析

- 新增 TextSplitter 类
- EntryService 支持逐条拆分分析
- 新增 BatchParseResponse 类型

feat(tags): 添加标签管理模块

- 新增 TagsController 和 TagService
- 新增 tags 和 entry_tags 数据表
```

### 3.2 分支策略

| 分支 | 用途 | 命名 |
|------|------|------|
| main | 生产代码 | `main` |
| 开发 | 日常开发 | `dev` |
| 功能 | 新功能开发 | `feature/xxx` |
| 修复 | Bug 修复 | `fix/xxx` |

### 3.3 推送规范

- 提交前确认 `dotnet build` 通过
- 不提交 `appsettings.json`（含 Secret）
- 不提交 `node_modules/`、`bin/`、`obj/`
- 提交 `.vue` 文件时确认无 `any` 类型
- 提交 `.sql` 文件时确认 schema 完整

---

## 四、配置文件管理

### 4.1 .gitignore

```gitignore
# 敏感配置（含 Secret）
server/appsettings.json
server/appsettings.Development.json

# 构建产物
server/bin/
server/obj/
web/dist/
web/node_modules/

# IDE
.idea/
.vscode/
*.swp

# 操作系统
.DS_Store
Thumbs.db

# 数据库迁移
server/Migrations/
```

### 4.2 appsettings.json 模板

```json
{
  "ConnectionStrings": {
    "MySql": "Server=127.0.0.1;Port=3306;Database=xiaoji;User=xiaoji;Password=CHANGE_ME;"
  },
  "Jwt": {
    "Secret": "CHANGE_ME_generate_a_secure_random_string_at_least_256_bits",
    "Issuer": "xiaoji",
    "Audience": "xiaoji-web"
  },
  "DeepSeek": {
    "ApiKey": "CHANGE_ME_your_deepseek_api_key",
    "BaseUrl": "https://api.deepseek.com",
    "Model": "deepseek-chat"
  },
  "Logging": {
    "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" }
  }
}
```

---

## 五、安全红线

以下情况禁止提交代码：

- [ ] 代码中硬编码 API Key、密码、Secret
- [ ] 使用 `as any`、`@ts-ignore`、`@ts-expect-error`
- [ ] 使用空 catch 块 `catch(e) {}`
- [ ] 前端 localStorage 存敏感信息
- [ ] 控制器中缺少 `[Authorize]` 标注
- [ ] EF Core 查询缺少 `Where(e => e.UserId == userId)`
- [ ] 标签操作缺少用户隔离（UserId 校验）
- [ ] 代码中出现 `Console.WriteLine` 调试残留
- [ ] SQL 拼接（防注入）
- [ ] 提交 `appsettings.json` 到 Git
- [ ] TextSplitter 无单元测试
- [ ] 批量解析未处理空输入

---

## 六、代码审查清单

### 后端审查
- [ ] `dotnet build --no-restore` 编译通过
- [ ] 所有控制器方法有正确的 HTTP 方法和路径
- [ ] 所有查询带 `user_id` 过滤
- [ ] TextSplitter 拆分逻辑单元测试覆盖
- [ ] 批量解析正确处理每条拆分结果
- [ ] 所有异常使用 `ApiException`
- [ ] 无硬编码 Secret
- [ ] EF Core 查询使用可翻译的 LINQ
- [ ] `Async` 后缀完整
- [ ] `CancellationToken` 参数齐全
- [ ] 标签操作验证用户归属

### 前端审查
- [ ] TypeScript 无 `any` 类型
- [ ] 所有 API 调用有类型标注
- [ ] Vue 组件使用 `<script setup lang="ts">`
- [ ] 异步函数有 try-catch/finally
- [ ] 401 跳转登录逻辑
- [ ] 批量结果显示正确
- [ ] 无魔法数字（使用常量）
- [ ] Vant 组件使用正确

---

## 七、工具链

| 工具 | 用途 | 使用方式 |
|------|------|---------|
| `dotnet build --no-restore` | 编译验证 | 每次提交前 |
| `lsp_diagnostics` | 诊断 | 改文件后 |
| Vite | 前端构建 | `npm run build` |

### Vite 构建命令

```bash
cd web && npm run dev      # 开发模式
cd web && npm run build    # 生产构建
```

### .NET 构建命令

```bash
dotnet build --no-restore  # 开发构建
dotnet test --no-restore   # 运行测试
dotnet publish -c Release -r linux-x64 --self-contained  # 生产发布
```

---

## 八、编码约定速查

### C# 速查
```csharp
// 文本拆分
var segments = TextSplitter.Split(rawText);

// 原始记录插入
var rawEntry = await db.RawEntries.AddAsync(new RawEntry { UserId = userId, RawText = segment });

// 批量响应构建
return new BatchParseResponse(results, results.Count, hint);

// 模式匹配
if (result is null) { ... }
entry.Type is EntryType.Income or EntryType.Expense

// 空合并
var baseUrl = config["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com";
```

### TypeScript 速查
```typescript
// 批量结果处理
for (const result of res.batchResults) {
  if (!result.needsConfirm) { ... }
  else if (result.reason === 'missing') { ... }
}

// 标签过滤
const filtered = await entryApi.list({ tagId: selectedTagId });

// 条件渲染
<van-button v-if="!submitting" @click="submit">提交</van-button>
<van-button v-else loading>处理中</van-button>
```
