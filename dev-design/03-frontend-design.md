# 晓记 · 前端页面详细设计

> 框架：Vue 3 + Vite + Vant 4
> 目标：移动端 H5，适配手机浏览器
> 设计规范：Vant 组件库 + 品牌配色（暖黄 #FFC940）

---

## 一、前端架构

```
web/src/
├─ main.ts
├─ App.vue                    # 根组件（Tabbar 壳）
├─ router/index.ts
├─ api/
│  ├─ http.ts                 # axios + JWT 拦截器
│  └─ index.ts                # 所有 API（含标签 API）
└─ views/
   ├─ LoginView.vue           # 登录/注册
   ├─ InputView.vue           # 口述录入（含批量结果显示）
   ├─ LedgerView.vue          # 账本（含标签筛选）
   ├─ ScheduleView.vue        # 日程
   ├─ KnowledgeView.vue       # 知识库（含标签过滤）
   ├─ AskView.vue             # 对话查询
   └─ TagsView.vue            # 标签管理（新增）
```

---

## 二、路由配置

| 路径 | 组件 | 说明 | Tabbar |
|------|------|------|--------|
| `/login` | LoginView.vue | 登录/注册 | ❌ |
| `/` | InputView.vue | 口述录入（首页） | ✅ |
| `/ledger` | LedgerView.vue | 账本 | ✅ |
| `/schedule` | ScheduleView.vue | 日程 | ✅ |
| `/knowledge` | KnowledgeView.vue | 知识库 | ✅ |
| `/ask` | AskView.vue | 对话查询 | ❌ |
| `/tags` | TagsView.vue | 标签管理 | ❌ |

---

## 三、全局样式

```css
:root {
  --van-primary-color: #ffc940;
  --van-tabbar-item-active-background: #fff8ec;
  --van-background-color: #fff8ec;
}
* { box-sizing: border-box; }
body {
  margin: 0;
  background: #fff8ec;
  font-family: -apple-system, 'PingFang SC', 'Microsoft YaHei', sans-serif;
  -webkit-tap-highlight-color: transparent;
}
```

---

## 四、页面详细设计

### 4.1 登录/注册页（LoginView.vue）

与之前设计一致，手机号+密码注册/登录。

### 4.2 口述录入页（InputView.vue）—— 核心页面

**布局**：
```
┌──────────────────┐
│  说一句，记一笔    │  ← van-nav-bar
├──────────────────┤
│                  │
│  ┌──────────────┐│
│  │              ││
│  │  文本输入区    ││  ← 支持输入多条（换行分隔）
│  │              ││
│  └──────────────┘│
│                  │
│  [ 说出来，记进去 ]│  ← 主按钮
│                  │
├──────────────────┤
│  处理结果          │  ← 批量结果展示（新增）
│ ┌──────────────┐ │
│ │ ✅ 进货  -200 │ │  ← 每条独立显示
│ │ ✅ 卖菜  +350 │ │
│ │ ⏳ 午餐  -15  │ │  ← 追问中的显示
│ └──────────────┘ │
├──────────────────┤
│  最近记录          │
└──────────────────┘
├──────────────────┤
│ [口述] [账本] [日程] [知识] │
└──────────────────┘
```

**批量结果展示逻辑**：

```typescript
// 处理批量解析结果
function handleBatchResponse(res: BatchParseResponse) {
  // 显示总条数
  showToast(`已为您拆分为 ${res.totalCount} 条记录`)

  // 每条结果独立处理
  for (const result of res.batchResults) {
    if (!result.needsConfirm) {
      showSuccessToast('已入账')
    } else if (result.reason === 'missing') {
      // 显示追问弹层
      showConfirmPopup(result)
    } else {
      showToast('AI 没接上，已挂起')
    }
  }
}
```

**追问弹层交互**：
- 弹层展示当前需要追问的那条记录的问题
- 用户补答后确认 → 重新解析 → 更新对应条目
- 弹层中每条问题对应一条记录的追问

**关键代码变化**：
```typescript
// 提交后处理批量结果
async function submit() {
  const text = rawText.value.trim()
  const res = await entryApi.parse(text)  // 返回 BatchParseResponse
  rawText.value = ''
  handleBatchResponse(res)
  await loadHistory()
}
```

---

### 4.3 账本页（LedgerView.vue）

**新增功能**：按标签筛选

**布局**：
```
┌──────────────────┐
│  账本              │  ← van-nav-bar
│  [📋 标签筛选 ▼]   │  ← 新增标签筛选按钮
├──────────────────┤
│ ┌──────────────┐ │
│ │ 2026年9月      │ │  ← 月份选择器
│ │ 收入 ¥5000.00  │ │
│ │ 支出 ¥3250.50  │ │
│ │ 结余 ¥1749.50  │ │
│ └──────────────┘ │
├──────────────────┤
│ ┌──────────────┐ │
│ │ 全部 | 支出 |收入│ │  ← 类型筛选
│ │ 标签: 苹果🟢   │ │  ← 当前选中的标签
│ └──────────────┘ │
│                  │
│ ┌──────────────┐ │
│ │ 9/22 进货  -200│ │  ← 带标签标识
│ │    标签: 苹果   │ │
│ ├──────────────┤ │
│ │ 9/22 卖菜  +350│ │
│ │    标签: 香蕉   │ │
│ └──────────────┘ │
└──────────────────┘
├──────────────────┤
│ [口述] [账本] [日程] [知识] │
└──────────────────┘
```

**标签筛选交互**：
- 点击"标签筛选"按钮弹出标签选择面板
- 支持多选标签（OR 逻辑：匹配任意选中标签）
- 选中后列表中显示标签标识

**API 调用**：
```typescript
// 获取带标签的列表
const list = await entryApi.list({ type, month, tagId })

// 获取可用标签
const tags = await tagApi.list()
```

---

### 4.4 日程页（ScheduleView.vue）

与之前设计一致，按时间倒序排列事件列表。新增显示标签信息。

---

### 4.5 知识库页（KnowledgeView.vue）

**新增功能**：标签过滤

**布局**：
```
┌──────────────────┐
│  知识库            │  ← van-nav-bar
├──────────────────┤
│ ┌──────────────┐ │
│ │ 🔍 搜索...     │ │
│ │ [标签过滤 ▼]   │ │  ← 新增标签过滤
│ └──────────────┘ │
│                  │
│ ┌──────────────┐ │
│ │ 🏷️ 苹果       │ │  ← 卡片带标签标识
│ │ 明天要交电费    │ │
│ │ 2026-09-20    │ │
│ ├──────────────┤ │
│ │ 🏷️ 学习心得    │ │
│ │ Python 笔记    │ │
│ └──────────────┘ │
└──────────────────┘
├──────────────────┤
│ [口述] [账本] [日程] [知识] │
└──────────────────┘
```

---

### 4.6 对话查询页（AskView.vue）

与之前设计一致，Chat 样式对话界面。

---

### 4.7 标签管理页（TagsView.vue）—— 新增

**布局**：
```
┌──────────────────┐
│  标签管理            │  ← van-nav-bar
├──────────────────┤
│ ┌──────────────┐ │
│ │ 📝 输入新标签   │ │  ← 输入框 + 添加按钮
│ └──────────────┘ │
│                  │
│ ┌──────────────┐ │
│ │ 苹果 ✕        │ │  ← 已创建的标签，可删除
│ │ 香蕉 ✕        │ │
│ │ 学习心得 ✕    │ │
│ └──────────────┘ │
│                  │
│ ┌──────────────┐ │
│ │ 按标签统计      │ │  ← 统计入口
│ │ 苹果: 5笔250元 │ │
│ └──────────────┘ │
└──────────────────┘
├──────────────────┤
│ [口述] [账本] [日程] [知识] │
└──────────────────┘
```

**交互逻辑**：
1. 顶部输入框输入标签名 → 点击"添加" → 调用 `POST /api/tags`
2. 同名标签不允许重复（后端 `UNIQUE KEY uk_user_name`）
3. 每个标签右侧有删除按钮 → 调用 `DELETE /api/tags/:id`
4. 底部显示"按标签统计"入口 → 跳转或展开统计面板

**前端代码结构**：
```typescript
<script setup lang="ts">
import { ref, onMounted } from 'vue'
import { tagApi, type TagDto } from '../api'

const newTagName = ref('')
const tags = ref<TagDto[]>([])

async function loadTags() {
  tags.value = await tagApi.list()
}

async function addTag() {
  const name = newTagName.value.trim()
  if (!name) return
  await tagApi.create(name)
  newTagName.value = ''
  await loadTags()
}

async function deleteTag(id: number) {
  await tagApi.delete(id)
  await loadTags()
}

onMounted(loadTags)
</script>
```

**在账本/知识库页添加标签**：

```typescript
// 给记录添加标签
async function addTagsToEntry(entryId: number, tagNames: string[]) {
  // 先获取标签 ID，如果不存在则创建
  for (const name of tagNames) {
    const tag = tags.value.find(t => t.name === name)
    if (!tag) {
      const newTag = await tagApi.create(name)
      await entryApi.addTags(entryId, [newTag.id])
    } else {
      await entryApi.addTags(entryId, [tag.id])
    }
  }
}
```

---

## 五、前端 API 封装

### 5.1 新增标签 API

```typescript
// web/src/api/index.ts

export interface TagDto {
  id: number
  userId: number
  name: string
  createdAt: string
}

export const tagApi = {
  list: () => http.get<TagDto[]>('/tags'),
  create: (name: string) => http.post<TagDto>('/tags', { name }),
  delete: (id: number) => http.delete<void>(`/tags/${id}`),
}

// 记录标签操作
export const entryApi = {
  // ... 原有 API
  addTags: (id: number, tagIds: number[]) =>
    http.post<void>(`/entries/${id}/tags`, { tagIds }),
  removeTag: (id: number, tagId: number) =>
    http.delete<void>(`/entries/${id}/tags/${tagId}`),
}
```

---

## 六、标签在全局的展示

在所有涉及记录的页面，标签统一展示为：

- 小标签 Chip 样式：`🏷️ 苹果`
- 颜色：品牌主色暖黄 #FFC940
- 点击标签可过滤该标签的所有记录

---

## 七、前端性能优化

| 策略 | 说明 |
|------|------|
| 批量解析结果 | 一次性展示所有结果，不逐条渲染 |
| 标签缓存 | 标签列表在页面间共享，不重复请求 |
| 列表截断 | 最多 200 条 |
| 静态缓存 | Vant 组件按需加载 |

---

## 八、前端注意事项

1. 标签管理页面是独立页面，通过路由 `/tags` 访问
2. 在账本/知识库页通过底部按钮或下拉菜单进入标签筛选
3. 所有 API 调用走 `/api` 前缀
4. Token 存在 localStorage，401 自动跳转登录
5. 批量结果展示时，每条独立显示状态（已入账/追问中/挂起）
6. 标签删除前确认（`showDialog` 或 `showToast` 提示）
