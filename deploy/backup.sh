#!/usr/bin/env bash
# 晓记 · 每日数据库备份
# mysqldump（单库，覆盖全部 6 张表：Users/RawEntries/Entries/ConfirmQas/Tags/EntryTags）
# → 保留最近 7 份
#
# 密码不写在脚本里：从环境变量读取（建议在 /opt/xiaoji/backup.env 中配置并由 cron source）
#   DB_USER=xiaoji
#   DB_PASS=***
#   DB_NAME=xiaoji
set -euo pipefail

ENV_FILE="${DB_ENV_FILE:-/opt/xiaoji/backup.env}"
if [ -f "$ENV_FILE" ]; then
  # shellcheck disable=SC1090
  . "$ENV_FILE"
fi

DB_USER="${DB_USER:-xiaoji}"
DB_NAME="${DB_NAME:-xiaoji}"
BACKUP_DIR="${BACKUP_DIR:-/opt/xiaoji/backups}"
KEEP="${KEEP:-7}"

if [ -z "${DB_PASS:-}" ]; then
  echo "错误：未设置 DB_PASS，请在 $ENV_FILE 中配置或导出环境变量" >&2
  exit 1
fi

mkdir -p "$BACKUP_DIR"
STAMP=$(date +%F-%H%M%S)
DUMP="$BACKUP_DIR/xiaoji-$STAMP.sql"

# 一致性备份（InnoDB 单事务，--routines 含存储过程，--triggers 默认已含）
mysqldump -u"$DB_USER" -p"$DB_PASS" \
  --single-transaction --quick --routines --events \
  --default-character-set=utf8mb4 \
  --set-gtid-purged=OFF \
  "$DB_NAME" | gzip > "$DUMP.gz"

# 空文件视为失败（磁盘满 / 密码错误时 mysqldump 会留下 0 字节产物）
if [ ! -s "$DUMP.gz" ]; then
  echo "错误：备份文件为空，疑似失败：$DUMP.gz" >&2
  exit 1
fi

# 清理过期备份（保留最近 KEEP 份）
ls -1t "$BACKUP_DIR"/xiaoji-*.sql.gz | tail -n +$((KEEP + 1)) | xargs -r rm -f

echo "备份完成: $DUMP.gz ($(du -h "$DUMP.gz" | cut -f1))"
