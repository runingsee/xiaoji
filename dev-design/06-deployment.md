# 晓记 · 部署与运维

> 目标：国内轻量服务器，Nginx(HTTPS) 反代 Kestrel，systemd 守护
> 部署方式：`dotnet publish` 发布单目录
> 数据库变更：schema.sql 新增 3 张表（raw_entries、tags、entry_tags）

---

## 一、服务器环境要求

| 项目 | 最低要求 | 推荐配置 | 说明 |
|------|---------|---------|------|
| CPU | 2 核 | 2 核 | |
| 内存 | 2 GB | 4 GB | MySQL + .NET 应用 |
| 磁盘 | 20 GB SSD | 40 GB SSD | 代码 + 数据库 + 备份 |
| 操作系统 | Linux (Ubuntu 22.04+) | Ubuntu 22.04 LTS | |
| .NET 运行时 | 不需要 | 不需要 | 独立发布 |
| MySQL | 8.0 | 8.0 | 已有 |

**内存规划**：MySQL ~500MB + .NET ~200MB ≈ 700MB
- 2C2G：需调小 `innodb_buffer_pool_size`（256M）
- 2C4G：完全从容

---

## 二、部署步骤

### 2.1 本地构建

```bash
# 后端发布
dotnet publish -c Release -r linux-x64 --self-contained -p:PublishSingleFile=true

# 前端构建
cd web && npm run build
```

### 2.2 上传服务器

```bash
# 后端
scp -r server/bin/Release/net10.0/linux-x64/publish/ user@server:/opt/xiaoji/

# 前端
scp -r web/dist/* user@server:/opt/xiaoji/wwwroot/
```

### 2.3 服务器目录结构

```
/opt/xiaoji/
├── xiaoji                    # .NET 可执行文件
├── wwwroot                   # 前端静态文件
├── config/
│   ├── appsettings.json      # 连接串/Secret
│   └── .gitignore
├── logs/                     # 应用日志
├── backups/                  # 数据库备份
└── data/                     # MySQL 数据
```

### 2.4 配置文件更新

**`/opt/xiaoji/config/appsettings.json`**：
```json
{
  "ConnectionStrings": {
    "MySql": "Server=127.0.0.1;Port=3306;Database=xiaoji;User=xiaoji;Password=<密码>;"
  },
  "Jwt": { "Secret": "...", "Issuer": "xiaoji", "Audience": "xiaoji-web" },
  "DeepSeek": { "ApiKey": "...", "BaseUrl": "https://api.deepseek.com", "Model": "deepseek-chat" },
  "Logging": { "LogLevel": { "Default": "Information", "Microsoft.AspNetCore": "Warning" } }
}
```

### 2.5 数据库初始化

```bash
# 执行新的 schema.sql（包含所有 6 张表）
mysql -u xiaoji -p xiaoji < /opt/xiaoji/config/schema.sql
```

### 2.6 启动与验证

```bash
# 手动启动测试
cd /opt/xiaoji
./xiaoji --urls "http://127.0.0.1:5000"

# 验证
curl http://127.0.0.1:5000/api/auth/me       # 返回 401（服务正常）
curl http://127.0.0.1:5000/api/tags          # 返回 401（标签 API 正常）
```

---

## 三、Nginx 配置

**新增路由**：`/api/tags` 和其他 API 路径自动反代到 Kestrel。

```nginx
server {
    listen 443 ssl http2;
    server_name xiaoji.example.com;

    ssl_certificate     /etc/letsencrypt/live/xiaoji.example.com/fullchain.pem;
    ssl_certificate_key /etc/letsencrypt/live/xiaoji.example.com/privkey.pem;
    ssl_protocols       TLSv1.2 TLSv1.3;

    add_header Strict-Transport-Security "max-age=31536000" always;
    add_header X-Content-Type-Options "nosniff" always;

    location / {
        root /opt/xiaoji/wwwroot;
        index index.html;
        try_files $uri $uri/ /index.html;
    }

    location /api/ {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;
        proxy_set_header Connection keep-alive;
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
        proxy_set_header X-Forwarded-For $proxy_add_x_forwarded_for;
        proxy_set_header X-Forwarded-Proto $scheme;
        proxy_read_timeout 90;
        proxy_connect_timeout 30;
    }
}

server {
    listen 80;
    server_name xiaoji.example.com;
    return 301 https://$host$request_uri;
}
```

---

## 四、Systemd 服务

```ini
[Unit]
Description=晓记 XiaoJi API Service
After=network.target mysqld.service
Requires=mysqld.service

[Service]
Type=exec
WorkingDirectory=/opt/xiaoji
ExecStart=/opt/xiaoji/xiaoji --urls http://127.0.0.1:5000 --configuration /opt/xiaoji/config/appsettings.json
Restart=always
RestartSec=5
StartLimitInterval=60
StartLimitBurst=3
NoNewPrivileges=true
PrivateTmp=true
ReadWritePaths=/opt/xiaoji/logs
StandardOutput=journal
StandardError=journal
SyslogIdentifier=xiaoji

[Install]
WantedBy=multi-user.target
```

```bash
# 安装与管理
sudo cp xiaoji.service /etc/systemd/system/
sudo systemctl daemon-reload
sudo systemctl enable xiaoji
sudo systemctl start xiaoji
sudo systemctl status xiaoji
sudo journalctl -u xiaoji -f
```

---

## 五、数据库备份

### 5.1 备份脚本

```bash
#!/bin/bash
BACKUP_DIR="/opt/xiaoji/backups"
DATE=$(date +%Y%m%d_%H%M%S)
KEEP_DAYS=7

mysqldump -u xiaoji -p'<密码>' xiaoji > "$BACKUP_DIR/xiaoji_$DATE.sql"
gzip "$BACKUP_DIR/xiaoji_$DATE.sql"
find "$BACKUP_DIR" -name "*.sql.gz" -mtime +$KEEP_DAYS -delete

echo "Backup completed: xiaoji_$DATE.sql.gz"
```

### 5.2 Crontab

```bash
# 每日凌晨 3 点备份
0 3 * * * /opt/xiaoji/deploy/backup.sh >> /opt/xiaoji/logs/backup.log 2>&1
```

### 5.3 备份恢复

```bash
gunzip < xiaoji_20260922_030000.sql.gz | mysql -u xiaoji -p xiaoji
```

---

## 六、数据库迁移方案

从当前架构迁移到新架构（含 raw_entries、tags、entry_tags）：

### 6.1 迁移脚本

```sql
-- 1. 创建新表（已在 schema.sql 中）
-- raw_entries, tags, entry_tags

-- 2. 将现有 entries 数据迁移到 raw_entries
-- 假设当前 entries 中每条记录都有对应的 raw_text
INSERT INTO raw_entries (user_id, raw_text, status, created_at)
SELECT user_id, raw_text, 'confirmed', created_at FROM entries;

-- 3. 更新 entries 表的 raw_entry_id
-- 由于 V1 开发阶段直接使用新 schema，此步骤在开发期不需要
-- 生产环境迁移时使用

-- 4. 验证数据一致性
SELECT COUNT(*) FROM raw_entries;
SELECT COUNT(*) FROM entries WHERE raw_entry_id IS NULL;
```

### 6.2 V1 开发策略

**直接使用新的 schema.sql**，不需要迁移现有数据。`schema.sql` 已包含所有 6 张表，开发阶段直接执行即可。

---

## 七、域名与证书

### 7.1 域名备案

| 步骤 | 说明 | 时间 |
|------|------|------|
| 购买域名 | 国内域名需备案 | 即时 |
| 备案 | 工信部备案 | 1-2 周 |
| 解析 | A 记录指向服务器 IP | 备案后 |

备案期间可先用 IP 直连开发。

### 7.2 免费 SSL

```bash
acme.sh --issue -d xiaoji.example.com --standalone
acme.sh --install-cert -d xiaoji.example.com \
    --key-file /etc/letsencrypt/live/xiaoji.example.com/privkey.pem \
    --fullchain-file /etc/letsencrypt/live/xiaoji.example.com/fullchain.pem
```

---

## 八、监控与告警

| 指标 | 工具 | 说明 |
|------|------|------|
| 服务存活 | `systemctl status xiaoji` | systemd 守护 |
| 端口监听 | `ss -tlnp | grep 5000` | Kestrel |
| Nginx 状态 | `systemctl status nginx` | |
| MySQL 状态 | `systemctl status mysqld` | |
| 备份状态 | `tail /opt/xiaoji/logs/backup.log` | 手动检查 |

V1 不做专业监控（Prometheus + Grafana），手动检查即可。

---

## 九、月度成本

| 项目 | 费用 | 说明 |
|------|------|------|
| MySQL | 0 | 已有 |
| 服务器 | 0 | 已有 |
| 域名 | ~4 元/月 | .com 域名分摊 |
| DeepSeek API | 个位数 | V1 用量极小 |
| **合计** | **10-20 元/月** | |

---

## 十、发布清单

- [ ] `dotnet publish -c Release -r linux-x64 --self-contained` 构建成功
- [ ] `web/dist` 构建成功
- [ ] `appsettings.json` 配置文件已放置（含 Secret）
- [ ] 新 `schema.sql` 已执行（6 张表全部创建）
- [ ] `xiaoji.service` 已安装并启用
- [ ] Nginx 配置正确，证书已申请
- [ ] HTTP → HTTPS 重定向生效
- [ ] `curl http://127.0.0.1:5000/api/auth/me` 返回 401
- [ ] `curl http://127.0.0.1:5000/api/tags` 返回 401
- [ ] 备份脚本已配置 crontab
- [ ] `systemctl status xiaoji` 显示 active (running)
- [ ] TextSplitter 单元测试通过
- [ ] 标签 API 联调通过
