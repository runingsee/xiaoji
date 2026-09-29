# 晓记 · 数据模型详细设计

> 数据库：MySQL 8 | 字符集：utf8mb4 | 排序规则：utf8mb4_unicode_ci
> 建库：`CREATE DATABASE xiaoji DEFAULT CHARSET utf8mb4 COLLATE utf8mb4_unicode_ci;`
> 包含 6 张表：users、raw_entries、entries、confirm_qas、tags、entry_tags

---

## 一、建库脚本

```sql
CREATE DATABASE IF NOT EXISTS xiaoji
  DEFAULT CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE xiaoji;
```

---

## 二、数据模型总览

```
┌───────────────────────────────────────────────────────────┐
│                         xiaoji 数据库                        │
│                                                           │
│  ┌──────────┐                                             │
│  │  users    │──1:N──┐                                    │
│  │  (用户)    │       ├── 1:N ──┐                         │
│  └──────────┘       │         │                           │
│                      ▼         ▼                           │
│              ┌───────────┐  ┌───────────┐                │
│              │ raw_entries│  │ tags      │ ← 用户标签     │
│              │ (原始记录) │  │ (标签)     │                │
│              │ 永不修改    │  └─────┬─────┘                │
│              └─────┬─────┘        │ N:M                    │
│                    │ 1:N          │                        │
│                    ▼             │                        │
│              ┌───────────┐      │                        │
│              │ entries    │      │                        │
│              │ (分析结果) │      │                        │
│              │ 可反复更新  │      │                        │
│              └─────┬─────┘      │                        │
│                    │            │                        │
│                    └──1:N──┐   │                        │
│                    ┌────────┴──┐              ┌─────────┴─────┐
│                    │confirm_qas│              │ entry_tags     │
│                    │(追问记录) │              │ (记录-标签关联) │
│                    └───────────┘              └───────────────┘
└───────────────────────────────────────────────────────────┘
```

**关系说明**：
- `users` 1:N `raw_entries`：一个用户多条原始记录
- `raw_entries` 1:N `entries`：一条原始记录拆分后可产生多条分析结果（想法四）
- `users` 1:N `tags`：一个用户拥有多个标签
- `entries` N:M `tags`：一条记录可有多个标签，一个标签可被多条记录使用（想法三）
- `entries` 1:N `confirm_qas`：一条记录可有多个追问记录

---

## 三、表结构详解

### 3.1 users（用户表）—— 不变

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| phone | VARCHAR(20) | NOT NULL, UNIQUE | 手机号 |
| password_hash | VARCHAR(100) | NOT NULL | BCrypt 哈希 |
| nickname | VARCHAR(50) | NULL | 昵称 |
| plan | VARCHAR(20) | NOT NULL, DEFAULT 'free' | 套餐 |
| plan_expires | DATETIME | NULL | 订阅到期 |
| created_at | DATETIME | NOT NULL, DEFAULT CURRENT_TIMESTAMP | 注册时间 |

```sql
CREATE TABLE users (
  id            BIGINT       NOT NULL AUTO_INCREMENT,
  phone         VARCHAR(20)  NOT NULL,
  password_hash VARCHAR(100) NOT NULL,
  nickname      VARCHAR(50)  NULL,
  plan          VARCHAR(20)  NOT NULL DEFAULT 'free',
  plan_expires  DATETIME     NULL,
  created_at    DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY uk_phone (Phone)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

### 3.2 raw_entries（原始记录表）—— 新增（想法一）

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| user_id | BIGINT | NOT NULL, FK→users(id) | 用户 ID |
| raw_text | TEXT | NOT NULL | **原始口述，永久保留，永不修改** |
| status | VARCHAR(20) | NOT NULL, DEFAULT 'pending' | pending=待分析；confirmed=已分析 |
| created_at | DATETIME | NOT NULL, DEFAULT CURRENT_TIMESTAMP | 创建时间 |

**索引**：`KEY idx_user (user_id)`

**实体类**：
```csharp
public sealed class RawEntry
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string RawText { get; set; } = "";
    public string Status { get; set; } = "pending";
    public DateTime CreatedAt { get; set; }
}
```

**设计要点**：
- 永不更新，只增不改
- `status` 标记是否已完成分析
- 拆分后的每条独立文本各自创建一条 `raw_entries` 记录
- `raw_text` 永远保留原始口述，保证可追溯性

### 3.3 entries（分析结果表）—— 重构（想法一 + 想法二）

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| raw_entry_id | BIGINT | NOT NULL, FK→raw_entries(id) | 关联的原始记录 |
| user_id | BIGINT | NOT NULL, FK→users(id) | 用户 ID |
| type | VARCHAR(20) | NOT NULL | expense / income / event / note |
| title | VARCHAR(200) | NOT NULL | AI 生成的一句话标题 |
| amount | DECIMAL(14,2) | NULL | 金额（元）；event/note 为 NULL |
| category | VARCHAR(50) | NULL | AI 自动分类 |
| occurred_at | DATETIME | NULL | 事件发生时间（支持补录） |
| summary | TEXT | NULL | AI 归纳摘要 |
| status | VARCHAR(20) | NOT NULL, DEFAULT 'confirmed' | confirmed/pending |
| created_at | DATETIME | NOT NULL, DEFAULT CURRENT_TIMESTAMP | 入库时间 |
| updated_at | DATETIME | NULL, ON UPDATE CURRENT_TIMESTAMP | 更新时间 |

**索引**：
- `KEY idx_user_time (user_id, occurred_at)` —— 账本/日程列表
- `KEY idx_user_type (user_id, type)` —— 分类过滤
- `KEY idx_raw_entry (raw_entry_id)` —— 按原始记录查

**实体类**：
```csharp
public sealed class Entry
{
    public long Id { get; set; }
    public long RawEntryId { get; set; }
    public long UserId { get; set; }
    public string Type { get; set; } = "";
    public string Title { get; set; } = "";
    public decimal? Amount { get; set; }
    public string? Category { get; set; }
    public DateTime? OccurredAt { get; set; }
    public string? Summary { get; set; }
    public string Status { get; set; } = "confirmed";
    public DateTime CreatedAt { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
```

**设计要点**：
- `raw_entry_id` 外键关联到 `raw_entries`，实现原始与分析分离
- 1 条 `raw_entries` 可对应 N 条 `entries`（多条拆分场景）
- `updated_at` 自动更新，记录修改历史
- `amount DECIMAL(14,2)` 精确存储，无浮点误差

### 3.4 confirm_qas（追问记录）—— 不变

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| entry_id | BIGINT | NOT NULL, FK→entries(id) ON DELETE CASCADE | 关联分析结果 |
| question | TEXT | NOT NULL | 系统问的 |
| answer | TEXT | NOT NULL | 用户答的 |
| created_at | DATETIME | NOT NULL, DEFAULT CURRENT_TIMESTAMP | 追问时间 |

```sql
CREATE TABLE IF NOT EXISTS confirm_qas (
  Id         BIGINT       NOT NULL AUTO_INCREMENT PRIMARY KEY,
  EntryId    BIGINT       NOT NULL,
  Question   TEXT         NOT NULL,
  Answer     TEXT         NOT NULL,
  CreatedAt  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_entry (EntryId),
  CONSTRAINT fk_qa_entry FOREIGN KEY (EntryId) REFERENCES Entries (Id) ON DELETE CASCADE
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
```

**设计要点**：
- `ON DELETE CASCADE`：删除 entries 时追问记录自动删除
- 追问关联 `entries`（分析结果），不是 `raw_entries`
- 用户和开发者可追溯完整的问答链

### 3.5 tags（标签表）—— 新增（想法三）

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| user_id | BIGINT | NOT NULL, FK→users(id) | 用户 ID，标签按用户隔离 |
| name | VARCHAR(50) | NOT NULL | 标签名，如"苹果"、"学习心得" |
| created_at | DATETIME | NOT NULL, DEFAULT CURRENT_TIMESTAMP | 创建时间 |

**索引**：`UNIQUE KEY uk_user_name (user_id, name)`

**实体类**：
```csharp
public sealed class Tag
{
    public long Id { get; set; }
    public long UserId { get; set; }
    public string Name { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
```

**设计要点**：
- `UNIQUE KEY uk_user_name (UserId, Name)`：同一用户下标签名唯一
- 用户 A 的"苹果"标签和用户 B 的"苹果"标签是独立的
- 支持用户自由创建、删除标签

### 3.6 entry_tags（记录-标签关联表）—— 新增（想法三）

| 列名 | 类型 | 约束 | 说明 |
|------|------|------|------|
| id | BIGINT | PK, AUTO_INCREMENT | 主键 |
| entry_id | BIGINT | NOT NULL, FK→entries(id) ON DELETE CASCADE | 分析结果 |
| tag_id | BIGINT | NOT NULL, FK→tags(id) ON DELETE CASCADE | 标签 |

**索引**：`UNIQUE KEY uk_entry_tag (entry_id, tag_id)`

**实体类**：
```csharp
public sealed class EntryTag
{
    public long Id { get; set; }
    public long EntryId { get; set; }
    public long TagId { get; set; }
}
```

**设计要点**：
- 多对多关系：一条记录可有多个标签，一个标签可被多条记录使用
- `ON DELETE CASCADE`：删除 entries 或 tags 时关联自动删除
- 复合唯一索引防止重复关联

---

## 四、EF Core 上下文配置

```csharp
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<RawEntry> RawEntries => Set<RawEntry>();
    public DbSet<Entry> Entries => Set<Entry>();
    public DbSet<ConfirmQa> ConfirmQas => Set<ConfirmQa>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<EntryTag> EntryTags => Set<EntryTag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // 用户
        modelBuilder.Entity<User>(b =>
        {
            b.HasIndex(u => u.Phone).IsUnique();
            b.Property(u => u.PasswordHash).IsRequired().HasMaxLength(100);
            b.Property(u => u.Plan).HasDefaultValue("free");
        });

        // 原始记录
        modelBuilder.Entity<RawEntry>(b =>
        {
            b.HasIndex(e => e.UserId);
            b.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
        });

        // 分析结果
        modelBuilder.Entity<Entry>(b =>
        {
            b.HasIndex(e => new { e.UserId, e.OccurredAt });
            b.HasIndex(e => new { e.UserId, e.Type });
            b.HasIndex(e => e.RawEntryId);
            b.HasOne(e => e.RawEntry).WithMany().HasForeignKey(e => e.RawEntryId);
            b.HasOne(e => e.User).WithMany().HasForeignKey(e => e.UserId);
            b.Property(e => e.Status).HasDefaultValue("confirmed");
            b.Property(e => e.Amount).HasColumnType("decimal(14,2)");
        });

        // 追问记录
        modelBuilder.Entity<ConfirmQa>(b =>
        {
            b.HasOne(q => q.Entry).WithMany(e => e.ConfirmQas)
                .HasForeignKey(q => q.EntryId).OnDelete(DeleteBehavior.Cascade);
        });

        // 标签
        modelBuilder.Entity<Tag>(b =>
        {
            b.HasIndex(t => new { t.UserId, t.Name }).IsUnique();
            b.HasOne(t => t.User).WithMany(u => u.Tags)
                .HasForeignKey(t => t.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        // 记录-标签关联
        modelBuilder.Entity<EntryTag>(b =>
        {
            b.HasKey(et => et.Id);
            b.HasOne(et => et.Entry).WithMany(e => e.EntryTags)
                .HasForeignKey(et => et.EntryId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(et => et.Tag).WithMany(t => t.EntryTags)
                .HasForeignKey(et => et.TagId).OnDelete(DeleteBehavior.Cascade);
        });
    }
}
```

---

## 五、字段约定规范

### 5.1 命名规范
- 列名：PascalCase（EF 默认映射）
- 实体类名：PascalCase 单数
- 数据库使用手工 SQL 建表，EF Core 仅做运行时增删改查

### 5.2 时间规范
- 全站使用 **Asia/Shanghai** 墙钟时间
- `created_at`：记录创建时自动填充
- `occurred_at`：事件实际发生时间（支持补录）
- `updated_at`：更新时间（MySQL `ON UPDATE CURRENT_TIMESTAMP`）
- API 交互格式：`yyyy-MM-dd` 或 `yyyy-MM-dd HH:mm`

### 5.3 金额规范
- 单位：**元**（不是分）
- 类型：`DECIMAL(14,2)`，精确存储
- 范围：最大 99,999,999,999.99 元

### 5.4 状态规范
- 所有状态字段用 VARCHAR(20)，不用 ENUM
- `raw_entries.status`：`pending` / `confirmed`
- `entries.status`：`confirmed` / `pending`

---

## 六、原始记录 → 分析结果的完整流程

### 6.1 单条录入

```
用户输入："今天进了200块的菜，卖了350"
    │
    ▼
EntryService.ParseAsync()
    │
    ├─ 1. INSERT raw_entries（raw_text = "今天进了200块的菜，卖了350"）
    │      → raw_entry_id = 1
    │
    ├─ 2. 调用 DeepSeek 分析 → ParseResult
    │
    ├─ 3. INSERT entries（raw_entry_id = 1, type='expense', title='进货', amount=200, ...）
    │
    └─ 4. 返回前端 ParseResponse（entry + 追问信息如有）
```

### 6.2 多条录入（想法四）

```
用户输入："今天进了200块的菜，卖了350\n中午吃饭花了15"
    │
    ▼
TextSplitter.split(rawText) → ["今天进了200块的菜，卖了350", "中午吃饭花了15"]
    │
    ▼
逐条处理（每条独立）：
    │
    ├─ segment1 → INSERT raw_entries → AI 分析 → INSERT entries
    │              raw_entry_id = 1, entries: [进货 200]
    │
    └─ segment2 → INSERT raw_entries → AI 分析 → INSERT entries
                   raw_entry_id = 2, entries: [午餐 15]
    │
    ▼
返回 BatchParseResponse（包含 2 条分析结果）
```

### 6.3 重解析流程

```
用户确认时追问/修改 → 更新 entries（不修改 raw_entries）
    │
    ├─ 重新调用 AI 分析
    ├─ 更新 entries 的 type/title/amount/category/summary
    ├─ 更新 updated_at
    └─ raw_entries.raw_text 保持不变，可追溯
```

---

## 七、性能设计

| 场景 | 优化手段 |
|------|---------|
| 账本列表 | `idx_user_time` 覆盖索引 |
| 分类过滤 | `idx_user_type` 覆盖索引 |
| 按标签统计 | `idx_tag` + JOIN 优化 |
| 月度汇总 | SQL 聚合 SELECT type + amount |
| 拆分处理 | 并行处理每条拆分结果 |
| 原始记录 | 仅存文本，查询量小 |
| 数据量 | 单用户年记录 < 5000（拆分后可能更多），MySQL 单表百万行内无忧 |

---

## 八、数据迁移方案

从当前 `entries` 结构迁移到新结构：

```sql
-- 步骤 1：创建新表
-- 步骤 2：将现有 entries 数据迁移
UPDATE entries e SET raw_entry_id = (
    SELECT id FROM raw_entries r WHERE r.id = e.id AND r.user_id = e.user_id
);
-- 步骤 3：添加外键约束
-- 步骤 4：创建 tags 和 entry_tags 表
-- 步骤 5：更新 EF Core 上下文
```

V1 开发阶段直接使用新 schema.sql，无需迁移。
