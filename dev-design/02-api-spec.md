# 晓记 · API 完整规范

> 基础 URL：`/api`
> 认证方式：JWT Bearer Token（除 `/api/auth` 外全部需要认证）
> 限流：10 次/10 秒/IP（登录/注册/解析接口）
> 响应格式：统一 JSON 结构

---

## 一、通用响应结构

### 成功响应

```json
{
  "code": 200,
  "message": "success",
  "data": { ... }
}
```

### 错误响应

```json
{
  "code": 400,
  "message": "手机号格式不正确"
}
```

### 限流响应

```json
{
  "code": 429,
  "message": "操作过于频繁，请稍后再试"
}
```

---

## 二、认证模块

### 2.1 注册

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/auth/register` |
| 限流 | ✅ 10次/10秒/IP |
| 认证 | ❌ 不需要 |

**请求体**：`{ "phone": "13800138000", "password": "123456", "nickname": "小明" }`

**成功响应（200）**：
```json
{
  "code": 200,
  "data": {
    "token": "eyJhbGciOiJIUzI1NiIs...",
    "user": { "id": 1, "phone": "13800138000", "nickname": "小明", "plan": "free", "createdAt": "2026-09-22" }
  }
}
```

**错误场景**：400（格式/长度）、409（手机号已注册）

---

### 2.2 登录

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/auth/login` |
| 限流 | ✅ 10次/10秒/IP |
| 认证 | ❌ 不需要 |

**成功响应（200）**：同上，包含 `token` 和 `user`

**错误场景**：401（手机号或密码错误）

---

### 2.3 获取当前用户信息

| 项 | 值 |
|------|------|
| 方法 | GET |
| 路径 | `/api/auth/me` |
| 认证 | ✅ 需 JWT |

成功响应包含用户完整信息。

---

## 三、记录模块（Entries）

### 3.1 口述解析（含批量拆分）—— 核心接口

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/entries/parse` |
| 限流 | ✅ 10次/10秒/IP |
| 认证 | ✅ 需 JWT |
| 说明 | 用户口述 → TextSplitter 拆分 → 逐条 AI 分析 → raw_entries + entries 入库 |

**请求体**：
```json
{ "rawText": "今天进了200块的菜，卖了350\n中午吃饭花了15" }
```

| 字段 | 类型 | 必填 | 说明 |
|------|------|------|------|
| rawText | string | ✅ | 用户口述文本，最长 500 字符 |

**处理流程**：
1. `TextSplitter.split(rawText)` 按规则拆分为多个独立文本
2. 每条独立文本创建一条 `raw_entries` 记录
3. 每条独立调用 AI 分析
4. 每条分析结果创建一条 `entries` 记录
5. 合并所有结果返回

**成功响应（200）**：

```json
{
  "code": 200,
  "data": {
    "batchResults": [
      {
        "entry": {
          "id": 1, "rawEntryId": 1, "type": "expense", "title": "进货",
          "amount": 200.00, "category": "进货", "occurredAt": "2026-09-22",
          "summary": "进了200块的菜", "status": "confirmed",
          "createdAt": "2026-09-22 14:30"
        },
        "needsConfirm": false
      },
      {
        "entry": {
          "id": 2, "rawEntryId": 1, "type": "income", "title": "卖菜",
          "amount": 350.00, "category": "经营收入", "occurredAt": "2026-09-22",
          "summary": "卖菜收入350元", "status": "confirmed",
          "createdAt": "2026-09-22 14:30"
        },
        "needsConfirm": false
      },
      {
        "entry": {
          "id": 3, "rawEntryId": 2, "type": "expense", "title": "午餐",
          "amount": 15.00, "category": "餐饮", "occurredAt": "2026-09-22",
          "summary": null, "status": "confirmed",
          "createdAt": "2026-09-22 14:30"
        },
        "needsConfirm": false
      }
    ],
    "totalCount": 3,
    "hint": "已为您拆分为 3 条记录，全部入账成功。"
  }
}
```

**追问场景**（某条需要追问）：

```json
{
  "code": 200,
  "data": {
    "batchResults": [
      {
        "entry": { "id": 1, ... },
        "needsConfirm": true,
        "reason": "missing",
        "questions": [{ "field": "occurredAt", "question": "这笔支出是今天发生的吗？" }],
        "draftToken": "abc123",
        "hint": "这条还差一些信息"
      },
      {
        "entry": { "id": 2, ... },
        "needsConfirm": false
      }
    ],
    "totalCount": 2,
    "hint": "第 1 条需要补充信息"
  }
}
```

**AI 不可用降级**：

```json
{
  "entry": { "id": 1, "type": "note", "title": "...", "status": "pending", ... },
  "needsConfirm": true,
  "reason": "parse_failed",
  "hint": "AI 暂时没接上，这条我先原样挂起来了"
}
```

**错误场景**：400（空文本）、429（限流）

---

### 3.2 确认入账

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/entries/confirm` |
| 认证 | ✅ 需 JWT |

**请求体（追问流程）**：
```json
{ "draftToken": "abc123", "answers": [{ "field": "occurredAt", "answer": "是今天发生的" }] }
```

**请求体（挂起流程）**：
```json
{ "entryId": 1, "clarification": "这是进货的支出" }
```

**成功响应（200）**：更新后的 entry + needsConfirm

**错误场景**：400（草稿过期）、404（记录不存在）

---

### 3.3 记录列表

| 项 | 值 |
|------|------|
| 方法 | GET |
| 路径 | `/api/entries` |
| 认证 | ✅ 需 JWT |

**请求参数**：

| 参数 | 类型 | 必填 | 说明 |
|------|------|------|------|
| type | string | ❌ | expense / income / event / note |
| month | string | ❌ | `yyyy-MM` |
| q | string | ❌ | 搜索关键词 |
| tagId | long | ❌ | 按标签 ID 过滤（想法三） |

**成功响应（200）**：
```json
{ "code": 200, "data": [{ "id": 1, "rawEntryId": 1, "type": "expense", ... }] }
```

**查询逻辑**：
- 默认按 `occurred_at` 降序
- 最多返回 200 条
- `tagId` 参数：JOIN entry_tags 表过滤

---

### 3.4 月度收支汇总

| 项 | 值 |
|------|------|
| 方法 | GET |
| 路径 | `/api/entries/summary` |
| 认证 | ✅ 需 JWT |

**成功响应**：
```json
{ "code": 200, "data": { "month": "2026-09", "income": 5000.00, "expense": 3250.50, "balance": 1749.50 } }
```

---

### 3.5 修改记录

| 项 | 值 |
|------|------|
| 方法 | PATCH |
| 路径 | `/api/entries/:id` |
| 认证 | ✅ 需 JWT |

**请求体**：`{ "type": "expense", "title": "修正", "amount": 200.00, "category": "进货", "occurredAt": "2026-09-22" }`

**错误场景**：404（不存在）、400（非法 type/时间格式）

---

### 3.6 删除记录

| 项 | 值 |
|------|------|
| 方法 | DELETE |
| 路径 | `/api/entries/:id` |
| 认证 | ✅ 需 JWT |

成功响应：204 No Content。删除 entries 会级联删除 confirm_qas 和 entry_tags，但不影响 raw_entries。

**错误场景**：404

---

## 四、标签模块（Tags）—— 新增（想法三）

### 4.1 获取用户标签列表

| 项 | 值 |
|------|------|
| 方法 | GET |
| 路径 | `/api/tags` |
| 认证 | ✅ 需 JWT |

**成功响应（200）**：
```json
{
  "code": 200,
  "data": [
    { "id": 1, "userId": 1, "name": "苹果", "createdAt": "2026-09-22" },
    { "id": 2, "userId": 1, "name": "学习心得", "createdAt": "2026-09-22" }
  ]
}
```

---

### 4.2 创建标签

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/tags` |
| 认证 | ✅ 需 JWT |

**请求体**：`{ "name": "香蕉" }`

**成功响应（200）**：创建的完整标签对象

**错误场景**：400（空名称）、409（同名标签已存在）

---

### 4.3 删除标签

| 项 | 值 |
|------|------|
| 方法 | DELETE |
| 路径 | `/api/tags/:id` |
| 认证 | ✅ 需 JWT |

删除标签后，`entry_tags` 中关联自动删除（ON DELETE CASCADE）。

---

### 4.4 给记录添加标签

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/entries/:id/tags` |
| 认证 | ✅ 需 JWT |

**请求体**：`{ "tagIds": [1, 2] }` 或 `{ "tagNames": ["苹果", "学习心得"] }`

说明：若标签不存在则自动创建（属于当前用户），然后关联到记录。

**错误场景**：404（记录不存在/不属于当前用户）

---

### 4.5 移除记录标签

| 项 | 值 |
|------|------|
| 方法 | DELETE |
| 路径 | `/api/entries/:id/tags/:tagId` |
| 认证 | ✅ 需 JWT |

成功响应：204 No Content。

---

### 4.6 按标签统计

| 项 | 值 |
|------|------|
| 方法 | GET |
| 路径 | `/api/tags/stats` |
| 认证 | ✅ 需 JWT |

**请求参数**：`month`（可选，格式 yyyy-MM）

**成功响应（200）**：
```json
{
  "code": 200,
  "data": [
    { "tagName": "苹果", "count": 5, "totalAmount": 250.00, "type": "expense" },
    { "tagName": "香蕉", "count": 3, "totalAmount": 120.00, "type": "expense" }
  ]
}
```

---

## 五、对话模块（Ask）

### 5.1 对话查询

| 项 | 值 |
|------|------|
| 方法 | POST |
| 路径 | `/api/ask` |
| 认证 | ✅ 需 JWT |

**请求体**：`{ "question": "我这个月总共赚了多少？" }`

**成功响应**：`{ "code": 200, "data": { "answer": "你这个月总共赚了 5000 元。" } }`

**错误场景**：400（问题为空）

**查询逻辑**：从 entries 表检索 Facts → RAG-lite → DeepSeek 作答。

---

## 六、错误码汇总

| 状态码 | 含义 | 使用场景 |
|--------|------|---------|
| 200 | 成功 | 所有成功响应 |
| 204 | 无内容 | DELETE 成功 |
| 400 | 参数错误 | 必填字段缺失、格式不对、非法值 |
| 401 | 未认证 | JWT 无效或过期 |
| 404 | 不存在 | 资源 ID 不存在或不属于当前用户 |
| 409 | 冲突 | 手机号已注册、标签已存在 |
| 429 | 限流 | 同一 IP 10 秒内超过 10 次请求 |
| 500 | 服务器内部错误 | 未捕获的异常 |

---

## 七、API 设计约定

### 7.1 路由前缀
- 所有 API 路径以 `/api` 开头
- 资源复数命名

### 7.2 HTTP 方法语义
| 方法 | 语义 | 幂等 |
|------|------|------|
| GET | 查询 | ✅ |
| POST | 创建/执行操作 | ❌ |
| PATCH | 部分更新 | ❌ |
| DELETE | 删除 | ✅ |

### 7.3 前端 API 封装

```typescript
// 新增标签 API
export const tagApi = {
  list: () => http.get<TagDto[]>('/tags'),
  create: (name: string) => http.post<TagDto>('/tags', { name }),
  delete: (id: number) => http.delete<void>(`/tags/${id}`),
};

export const entryApi = {
  parse: (rawText: string) => http.post<BatchParseResponse>('/entries/parse', { rawText }),
  confirm: (payload: ConfirmEntryRequest) => http.post<BatchParseResponse>('/entries/confirm', payload),
  list: (params: { type?: string; month?: string; q?: string; tagId?: number }) =>
    http.get<EntryDto[]>('/entries', { params }),
  summary: (month?: string) => http.get<MonthSummary>('/entries/summary', { params: month ? { month } : {} }),
  addTags: (id: number, tagIds: number[]) => http.post<void>(`/entries/${id}/tags`, { tagIds }),
  removeTag: (id: number, tagId: number) => http.delete<void>(`/entries/${id}/tags/${tagId}`),
};
```

### 7.4 BatchParseResponse 类型

```typescript
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
```
