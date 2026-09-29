# 晓记 · AI 链路详细设计

> AI 服务：DeepSeek API（deepseek-chat 模型）
> 新增：TextSplitter 服务端文本拆分层（想法四）
> 核心设计：AI 只在录入时调用，统计走 SQL

---

## 一、文本拆分层（TextSplitter）

### 1.1 设计目标

用户一次性输入可能包含多条记录，需要在 AI 分析前将输入拆分为独立文本，每条独立走完整分析流程。

### 1.2 拆分规则

| 优先级 | 分隔符 | 说明 | 示例 |
|--------|--------|------|------|
| 1 | `\n`（换行） | 最明确的拆分标记 | "第一行\n第二行" |
| 2 | `；`（中文分号） | "一句一条"场景 | "进了200；卖了350" |
| 3 | `、`（中文顿号） | 并列列举 | "苹果、香蕉、橘子" |
| 4 | `。`（中文句号） | 完整句子结束 | "今天进货了。卖了很多。" |
| ❌ 不拆 | `，`（逗号） | 逗号可能在单条记录内部 | "苹果、香蕉" 不拆 |

**额外约束**：
- 拆分后每条至少 5 个字符
- 总条数上限 10 条
- 拆分后去除空白字符

### 1.3 实现代码

```csharp
// server/Data/TextSplitter.cs

public static class TextSplitter
{
    private const int MinSegmentLength = 5;
    private const int MaxSegments = 10;

    /// <summary>
    /// 将用户输入拆分为多条独立文本。
    /// 优先按换行拆分，其次按中文分隔符。
    /// </summary>
    public static List<string> Split(string rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText))
            return [];

        var trimmed = rawText.Trim();

        // 优先按换行拆分
        var lines = trimmed.Split(['\n', '\r'], StringSplitOptions.RemoveEmptyEntries);
        if (lines.Length > 1)
        {
            return lines
                .Select(l => l.Trim())
                .Where(l => l.Length >= MinSegmentLength)
                .Take(MaxSegments)
                .ToList();
        }

        // 其次按中文分隔符拆分
        var parts = trimmed.Split(['；', '、', '。'], StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length > 1)
        {
            return parts
                .Select(p => p.Trim())
                .Where(p => p.Length >= MinSegmentLength)
                .Take(MaxSegments)
                .ToList();
        }

        // 不拆分：整条文本作为唯一 segment
        return [trimmed];
    }

    /// <summary>
    /// 判断是否需要拆分
    /// </summary>
    public static bool NeedsSplit(string rawText) => Split(rawText).Count > 1;
}
```

### 1.4 拆分示例

| 用户输入 | 拆分结果 | 说明 |
|---------|---------|------|
| "今天进了200块的菜，卖了350\n中午吃饭花了15" | ["今天进了200块的菜，卖了350", "中午吃饭花了15"] | 按换行 |
| "进了200的菜；卖了350；中午花了15" | ["进了200的菜", "卖了350", "中午花了15"] | 按分号 |
| "今天买了苹果和香蕉" | ["今天买了苹果和香蕉"] | 不拆（无分隔符） |
| "今天买了苹果、香蕉" | ["今天买了苹果", "香蕉"] | 按顿号 |
| "" | [] | 空输入 |
| "短" | [] | 不足 5 字符 |
| "a\nb\nc\nd\ne\nf\ng\nh\ni\nj\nk" | 前 10 条 | 上限 10 条 |

---

## 二、DeepSeek API 接入架构

### 2.1 HttpClient 配置

```csharp
builder.Services.AddHttpClient<DeepSeekClient>(c =>
{
    c.BaseAddress = new Uri(config["DeepSeek:BaseUrl"] ?? "https://api.deepseek.com");
    c.Timeout = TimeSpan.FromSeconds(60);
});
```

### 2.2 JSON 序列化配置

```csharp
private static readonly JsonSerializerOptions JsonOpts = new()
{
    PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
    PropertyNameCaseInsensitive = true,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
};
```

---

## 三、口述解析链路（核心）

### 3.1 完整流程（含多条拆分）

```
用户输入 rawText
    │
    ▼
TextSplitter.Split(rawText)
    │
    ├── [segment1] → Insert RawEntry → AI Parse → Insert Entry → 追问？
    ├── [segment2] → Insert RawEntry → AI Parse → Insert Entry → 追问？
    └── ...（每条独立处理）
    │
    ▼
返回 BatchParseResponse
```

### 3.2 核心代码（EntryService 改造）

```csharp
public async Task<BatchParseResponse> ParseAsync(long userId, string rawText, CancellationToken ct)
{
    if (string.IsNullOrWhiteSpace(rawText))
        throw ApiException.BadRequest("请先说点什么再记");

    // 1. 文本拆分
    var segments = TextSplitter.Split(rawText);

    // 2. 逐条处理（每条独立 AI 分析）
    var results = new List<BatchParseResult>();
    foreach (var segment in segments)
    {
        var result = await ParseSingleAsync(userId, segment, ct);
        results.Add(result);
    }

    // 3. 合并返回
    var totalConfirmed = results.Count(r => !r.NeedsConfirm);
    var hint = results.Count == 1
        ? null
        : $"已为您拆分为 {results.Count} 条记录，{totalConfirmed} 条已入账";

    return new BatchParseResponse(results, results.Count, hint);服
}

/// <summary>
/// 单条解析（核心逻辑）
/// </summary>
private async Task<BatchParseResult> ParseSingleAsync(long userId, string segment, CancellationToken ct)
{
    // 1. 插入 raw_entries
    var rawEntry = await InsertRawEntryAsync(userId, segment, ct);

    // 2. AI 解析（一次失败重试）
    var result = await ai.ParseAsync(segment, ct) ?? await ai.ParseAsync(segment, ct);

    // 3. AI 不可用：原样挂起
    if (result is null)
    {
        var entry = await InsertStubEntryAsync(userId, rawEntry.Id, segment, ct);
        return new BatchParseResult(entry, NeedsConfirm: true, Reason: "parse_failed",
            Hint: "AI 暂时没接上，这条我先原样挂起来了。");
    }

    // 4. 信息完整：直接入库
    if (result.MissingList.Count == 0)
    {
        var entry = await InsertConfirmedEntryAsync(userId, rawEntry.Id, segment, result, ct);
        return new BatchParseResult(entry, NeedsConfirm: false);
    }

    // 5. 信息缺失：发追问 + 草稿票
    var token = Guid.NewGuid().ToString("N");
    drafts.Set(token, new DraftState(segment, result, []), DraftTtl);
    return new BatchParseResult(null, NeedsConfirm: true, Reason: "missing",
        Questions: result.MissingList, DraftToken: token, Draft: result,
        Hint: "还有几个信息没弄清楚，帮我补齐一下就入账。");
}
```

### 3.3 重试策略

| 环节 | 策略 | 原因 |
|------|------|------|
| AI 解析失败 | 自动重试 1 次 | 网络抖动 |
| 2 次均失败 | 降级为 pending | 不阻塞用户 |
| 追问流程中重解析失败 | 重试 1 次 | 同上 |
| 仍失败 | 按 note 入库兜底 | 不再无限循环 |

### 3.4 草稿票据机制

```csharp
private static readonly TimeSpan DraftTtl = TimeSpan.FromMinutes(10);
drafts.Set(token, new DraftState(rawText, result, []), DraftTtl);
```

- 每条拆分结果独立草稿票
- IMemoryCache TTL 10 分钟
- 重启后草稿丢失（瞬态数据）

---

## 四、追问机制（AI Plan 模式）

### 4.1 每条记录独立追问

```
第一条记录：missing → 追问 → 用户补答 → 重新解析 → 入库
第二条记录：信息完整 → 直接入库
第三条记录：parse_failed → 挂起
```

### 4.2 确认入账流程

```
用户补答 → POST /api/entries/confirm
    │
    ├── draftToken 存在 → 读取草稿 → 合并文本+回答 → 重解析 → 入库
    └── entryId 存在 → 读取 pending 条目 → 合并澄清文本 → 重解析 → 入库
```

### 4.3 追问次数限制

V1 最多 **两轮追问**：
- 第 1 轮：首次解析 → 发追问
- 第 2 轮：用户补答 → 再次解析
- 第 3 轮：仍有缺失 → 不再追问，按 note 入库兜底

---

## 五、对话查询（RAG-lite）

### 5.1 Facts 构建

```csharp
// 从 entries 表检索（分析结果，不是 raw_entries）
var monthRows = await db.Entries
    .Where(e => e.UserId == userId && e.OccurredAt >= monthStart && e.OccurredAt < monthEnd
                && (e.Type == EntryType.Income || e.Type == EntryType.Expense))
    .Select(e => new MonthRow(e.Type, e.Amount, e.Category, e.Title))
    .ToListAsync(ct);
```

### 5.2 Facts 组装

```csharp
private static List<ChatFact> BuildFacts(DateTime now, DateTime monthStart,
    List<MonthRow> monthRows, List<Entry> recent, List<Entry> upcoming)
{
    var facts = new List<ChatFact>();

    var income = monthRows.Where(r => r.Type == EntryType.Income).Sum(r => r.Amount ?? 0);
    var expense = monthRows.Where(r => r.Type == EntryType.Expense).Sum(r => r.Amount ?? 0);
    facts.Add(new ChatFact("month_summary", $"本月（{monthStart:yyyy-MM}）",
        $"收入 {income:0.##} 元，支出 {expense:0.##} 元，结余 {income - expense:0.##} 元"));

    facts.AddRange(GroupByCategory("income", monthRows.Where(r => r.Type == EntryType.Income)));
    facts.AddRange(GroupByCategory("expense", monthRows.Where(r => r.Type == EntryType.Expense)));

    // 最近 15 条记录
    foreach (var e in recent)
    {
        facts.Add(new ChatFact("record", $"{e.Title}（{e.Type}）",
            $"{e.OccurredAt:yyyy-MM-dd}  {(e.Amount is not null ? e.Amount.Value.ToString("0.##") + " 元" : "")}"));
    }

    // 即将发生的 10 条日程
    foreach (var e in upcoming)
    {
        facts.Add(new ChatFact("schedule", $"{e.Title}",
            $"{e.OccurredAt:yyyy-MM-dd HH:mm}{(string.IsNullOrWhiteSpace(e.Summary) ? "" : "，" + e.Summary)}"));
    }

    return facts;
}
```

### 5.3 Prompt 安全

```csharp
public static string AskSystem(string factsJson) => $$"""
    你是用户个人的记账秘书「晓记」。

    下面是用户数据库中的真实数据 Facts（金额单位均为"元"）：
    {{factsJson}}

    回答规则：
    1. 只能基于 Facts 回答，严禁编造 Facts 中不存在的数字、日期或事项。
    2. 数据不足时如实说明，不要强行猜测。
    3. 回答口语化、简洁，像邻家秘书说话。
    4. 用户问的是总结/趋势时，可以直接对 Facts 做加法、求合计。
    """;
```

---

## 六、标签与 AI 的配合（想法三）

### 6.1 标签不影响 AI 分析

- 标签是用户手动添加的，不参与 AI 解析流程
- AI 解析时只使用 `ParseSystem` prompt 中的分类逻辑
- 标签在入库后由用户手动管理

### 6.2 标签辅助统计

- 账本页面支持按标签过滤
- `GET /api/tags/stats` 按标签聚合统计
- 标签维度统计完全走 SQL，不调 AI

### 6.3 标签创建逻辑

```csharp
// 给记录添加标签时
public async Task AddTagsToEntry(long entryId, List<string> tagNames, long userId)
{
    foreach (var name in tagNames)
    {
        // 查找用户已有的标签
        var tag = await db.Tags.FirstOrDefaultAsync(
            t => t.UserId == userId && t.Name == name, ct);

        if (tag is null)
        {
            // 不存在则创建
            tag = new Tag { UserId = userId, Name = name };
            db.Tags.Add(tag);
            await db.SaveChangesAsync(ct);
        }

        // 关联到记录
        if (!db.EntryTags.Any(et => et.EntryId == entryId && et.TagId == tag.Id))
        {
            db.EntryTags.Add(new EntryTag { EntryId = entryId, TagId = tag.Id });
        }
    }
    await db.SaveChangesAsync(ct);
}
```

---

## 七、AI 调用错误处理

### 7.1 错误分类

| 错误类型 | 表现 | 处理方式 |
|---------|------|---------|
| 网络错误 | HttpRequestException | 重试 1 次 |
| 超时 | TaskCanceledException | 重试 1 次 |
| 非法 JSON | JsonException | 降级为 pending |
| API 返回非 200 | HTTP 状态码错误 | 记录日志，降级 |
| 返回空内容 | content 为空/null | 降级 |

### 7.2 降级策略

```
AI 调用失败
    ├─ 第一次失败 → 重试
    ├─ 第二次失败 → 创建 pending 条目（type=note）
    │                  └─ raw_text 存入 raw_entries
    │                  └─ entries 中 status=pending
    └─ 用户确认时 → 再次尝试 AI 解析
                       ├─ 成功 → 更新 entries
                       └─ 仍失败 → 按 note 入库兜底
```

### 7.3 日志记录

```csharp
_logger.LogWarning("DeepSeek 解析未返回内容: {Raw}", Truncate(rawText));
_logger.LogWarning(ex, "DeepSeek 返回非法 JSON: {Content}", Truncate(content));
_logger.LogWarning("DeepSeek 调用失败 {Status}: {Body}", (int)resp.StatusCode, Truncate(body));
```

---

## 八、AI 监控指标

| 指标 | 说明 | 告警阈值 |
|------|------|---------|
| AI 调用成功率 | ParseAsync 成功率 | < 90% |
| AI 平均响应时间 | DeepSeek 响应耗时 | > 30s |
| 拆分比例 | 需要拆分的输入比例 | > 80%（说明 prompt 需优化） |
| 追问率 | 需要追问的比例 | > 50%（说明 prompt 需优化） |
| pending 比例 | AI 降级挂起比例 | > 10% |
| 平均拆分条数 | 每次输入平均拆为几条 | > 5（提示优化输入引导） |

---

## 九、AI 安全设计

- 用户输入只在 `user` 角色消息中
- system prompt 固定不变
- 对话查询 prompt 明确"仅依据 Facts"
- DeepSeek Key 只在后端
- 不允许用户输入影响 system prompt
- Prompt 注入防护：所有用户输入独立传递

---

## 十、V2 AI 演进方向

| 方向 | 说明 | 优先级 |
|------|------|--------|
| 语音录入 | 接入语音模型 | 高 |
| 向量数据库 | 替换 RAG-lite | 中 |
| 智能分类 | 根据标签历史优化 category 判断 | 中 |
| 图片 OCR | V3 | 低 |
| 离线模型 | 本地小模型 | 低 |
