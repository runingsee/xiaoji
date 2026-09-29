-- ============================================================================
-- 晓记 V1 数据库结构
-- MySQL 8.x | 建库: xiaoji | 字符集: utf8mb4
-- 执行: mysql -u <账号> -p < schema.sql
-- 注意: 列名采用 PascalCase 与 EF Core 默认映射保持一致（勿改）
-- 包含 6 张表：Users, RawEntries, Entries, ConfirmQas, Tags, EntryTags
-- ============================================================================

CREATE DATABASE IF NOT EXISTS xiaoji
  DEFAULT CHARACTER SET utf8mb4
  COLLATE utf8mb4_unicode_ci;

USE xiaoji;

-- ----------------------------------------------------------------------------
-- 用户（plan / plan_expires 为 V2 收费预留，V1 恒为 free）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Users (
  Id            BIGINT       NOT NULL AUTO_INCREMENT,
  Phone         VARCHAR(20)  NOT NULL,
  PasswordHash  VARCHAR(100) NOT NULL COMMENT 'BCrypt',
  Nickname      VARCHAR(50)  NULL,
  Plan          VARCHAR(20)  NOT NULL DEFAULT 'free',
  PlanExpires   DATETIME     NULL,
  CreatedAt     DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY uk_phone (Phone)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- ----------------------------------------------------------------------------
-- 原始记录表（用户口述，永久保留，永不修改）
-- Status: pending=待分析 confirmed=已分析
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS RawEntries (
  Id          BIGINT       NOT NULL AUTO_INCREMENT,
  UserId      BIGINT       NOT NULL,
  RawText     TEXT         NOT NULL COMMENT '原始口述，永久保留，可追溯',
  Status      VARCHAR(20)  NOT NULL DEFAULT 'pending',
  CreatedAt   DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_user (UserId),
  CONSTRAINT fk_rawentries_user FOREIGN KEY (UserId) REFERENCES Users (Id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- ----------------------------------------------------------------------------
-- 分析结果表（AI 解析后的结构化结果，可反复更新）
-- 关联到 RawEntries，实现原始与分析分离
-- Type: expense=支出 income=收入 event=日程事件 note=知识碎片
-- Status: confirmed=已入库 pending=解析失败挂起
-- OccurredAt 存"事件发生时间"（支持补录），CreatedAt 存"录入时间"
-- 时间约定：全站使用北京时间（Asia/Shanghai）墙钟时间，不涉时区换算
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Entries (
  Id           BIGINT        NOT NULL AUTO_INCREMENT,
  RawEntryId   BIGINT        NOT NULL COMMENT '关联的原始记录',
  UserId       BIGINT        NOT NULL,
  Type         VARCHAR(20)   NOT NULL,
  Title        VARCHAR(200)  NOT NULL,
  Amount       DECIMAL(14,2) NULL,
  Category     VARCHAR(50)   NULL,
  OccurredAt   DATETIME      NULL,
  Summary      TEXT          NULL,
  Status       VARCHAR(20)   NOT NULL DEFAULT 'confirmed',
  CreatedAt    DATETIME      NOT NULL DEFAULT CURRENT_TIMESTAMP,
  UpdatedAt    DATETIME      NULL DEFAULT NULL ON UPDATE CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_user_time (UserId, OccurredAt),
  KEY idx_user_type (UserId, Type),
  KEY idx_raw_entry (RawEntryId),
  CONSTRAINT fk_entries_rawentry FOREIGN KEY (RawEntryId) REFERENCES RawEntries (Id) ON DELETE CASCADE,
  CONSTRAINT fk_entries_user FOREIGN KEY (UserId) REFERENCES Users (Id)
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- ----------------------------------------------------------------------------
-- 智能追问记录（可追溯）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ConfirmQas (
  Id         BIGINT       NOT NULL AUTO_INCREMENT,
  EntryId    BIGINT       NOT NULL,
  Question   TEXT         NOT NULL COMMENT '系统问的',
  Answer     TEXT         NOT NULL COMMENT '用户答的',
  CreatedAt  DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  KEY idx_entry (EntryId),
  CONSTRAINT fk_qa_entry FOREIGN KEY (EntryId) REFERENCES Entries (Id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- ----------------------------------------------------------------------------
-- 标签表（按用户隔离，每个用户独立标签体系）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS Tags (
  Id        BIGINT       NOT NULL AUTO_INCREMENT,
  UserId    BIGINT       NOT NULL,
  Name      VARCHAR(50)  NOT NULL,
  CreatedAt DATETIME     NOT NULL DEFAULT CURRENT_TIMESTAMP,
  PRIMARY KEY (Id),
  UNIQUE KEY uk_user_name (UserId, Name),
  CONSTRAINT fk_tags_user FOREIGN KEY (UserId) REFERENCES Users (Id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;

-- ----------------------------------------------------------------------------
-- 记录-标签关联表（多对多）
-- ----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS EntryTags (
  Id      BIGINT NOT NULL AUTO_INCREMENT,
  EntryId BIGINT NOT NULL,
  TagId   BIGINT NOT NULL,
  PRIMARY KEY (Id),
  UNIQUE KEY uk_entry_tag (EntryId, TagId),
  KEY idx_tag (TagId),
  CONSTRAINT fk_entrytags_entry FOREIGN KEY (EntryId) REFERENCES Entries (Id) ON DELETE CASCADE,
  CONSTRAINT fk_entrytags_tag FOREIGN KEY (TagId) REFERENCES Tags (Id) ON DELETE CASCADE
) ENGINE = InnoDB DEFAULT CHARSET = utf8mb4 COLLATE = utf8mb4_unicode_ci;
