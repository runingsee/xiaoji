# 晓记 · 服务器部署手册（国内轻量服 / Linux）

> 目标机器：2C2G 轻量服务器，已装 MySQL 8。以下命令以 Ubuntu/Debian 为例。

## 0. 前置准备

- 域名已备案（阿里云/腾讯云控制台提交，1-2 周）
- 本地装好 .NET 10 SDK（开发机）
- 服务器装好：Nginx、curl（MySQL 若未装：`apt install mysql-server`）

## 1. MySQL：建库 + 专用账号 + 2G 内存调优

```bash
# 1) 建库建表（上传 server/Data/schema.sql 后执行）
mysql -u root -p < schema.sql

# 2) 专用低权限账号（不要复用 root）
mysql -u root -p -e "
CREATE USER 'xiaoji'@'localhost' IDENTIFIED BY '强密码';
GRANT SELECT, INSERT, UPDATE, DELETE ON xiaoji.* TO 'xiaoji'@'localhost';
FLUSH PRIVILEGES;"
```

**2G 内存必须做（否则 MySQL 默认配置 + 应用可能吃光内存）：** 编辑 `/etc/mysql/mysql.conf.d/mysqld.cnf`，在 `[mysqld]` 下追加：

```ini
innodb_buffer_pool_size = 256M
innodb_log_file_size = 64M
max_connections = 100
performance_schema = OFF
```

```bash
systemctl restart mysql
```

验证：`free -h` 确认内存余量。

## 2. 配置密钥（服务器上手动放置，永不进 git）

```bash
mkdir -p /opt/xiaoji
nano /opt/xiaoji/appsettings.Production.json
```

> 也可直接复制仓库里的模板：`cp server/appsettings.Example.json /opt/xiaoji/appsettings.Production.json`

内容（真实值替换）：

```json
{
  "ConnectionStrings": {
    "MySql": "Server=127.0.0.1;Port=3306;Database=xiaoji;User=xiaoji;Password=强密码;"
  },
  "Jwt": {
    "Secret": "用 openssl rand -base64 48 生成，至少32位",
    "Issuer": "xiaoji",
    "Audience": "xiaoji-web"
  },
  "DeepSeek": {
    "ApiKey": "sk-实际Key",
    "BaseUrl": "https://api.deepseek.com",
    "Model": "deepseek-chat"
  }
}
```

```bash
# systemd 以 www-data 身份运行，应用目录与配置文件需对该用户可读
chmod 755 /opt/xiaoji /opt/xiaoji/app
chmod 640 /opt/xiaoji/appsettings.Production.json
chown root:www-data /opt/xiaoji/appsettings.Production.json
```

## 3. 本地发布并上传

```bash
# 开发机
dotnet publish -c Release -r linux-x64 --self-contained
# 得到 server/bin/Release/net10.0/linux-x64/publish/

# 上传整个 publish 目录到 /opt/xiaoji/app（scp 或宝塔面板均可）
```

前端构建产物复制进 `publish/wwwroot/`（见 deploy/安装前端 一节）。

## 4. systemd 守护

```bash
cp deploy/xiaoji.service /etc/systemd/system/xiaoji.service
# 核对文件里的路径与你实际目录一致（/opt/xiaoji/app）
systemctl daemon-reload
systemctl enable --now xiaoji
systemctl status xiaoji
```

## 5. Nginx + HTTPS

```bash
# 装证书：acme.sh 或云厂商免费证书，证书文件放 /etc/nginx/ssl/
cp deploy/nginx.conf /etc/nginx/sites-available/xiaoji
ln -s /etc/nginx/sites-available/xiaoji /etc/nginx/sites-enabled/
nginx -t && systemctl reload nginx
```

## 6. 每日备份

```bash
# 备份用的数据库密码独立放在 env 文件（不进 git，权限收紧）
cat > /opt/xiaoji/backup.env <<'EOF'
DB_USER=xiaoji
DB_PASS=强密码（与 appsettings.Production.json 一致）
DB_NAME=xiaoji
EOF
chmod 600 /opt/xiaoji/backup.env

cp deploy/backup.sh /opt/xiaoji/backup.sh
chmod +x /opt/xiaoji/backup.sh
/opt/xiaoji/backup.sh          # 先手动跑一次验证能出文件

crontab -e
# 每天 02:30 执行：
30 2 * * * /opt/xiaoji/backup.sh
```

> 恢复演练（每月一次）：`gunzip -c /opt/xiaoji/backups/xiaoji-*.sql.gz | mysql -u xiaoji -p xiaoji`

**建议每周把 `/opt/xiaoji/backups/` 下载一份到本地**（备份在服务器上是抗不了服务器整个挂掉的）。

## 7. 更新发布流程（以后每次迭代）

1. 本地 `dotnet publish`（同上）
2. 上传替换 `/opt/xiaoji/app/` 内容（保留 `wwwroot` 与 `appsettings.Production.json`）
3. `dotnet publish` 时前端 dist 重新复制进 `wwwroot`
4. `systemctl restart xiaoji`

---

### 自检清单

- [ ] MySQL 只监听 127.0.0.1（默认），不对公网开放 3306
- [ ] Nginx 443 正常，80 跳转 443
- [ ] `curl -k https://你的域名/api/auth/me` 返回 401（而不是 404/500）→ 服务与 JWT 链路正常
- [ ] 备份 cron 已生效，`/opt/xiaoji/backups/` 有当日文件
- [ ] `git log -1 --stat` 确认仓库内无 `appsettings*.json` 真实密钥（模板 `.Example.json` 除外）
- [ ] `systemctl show xiaoji -p MemoryMax` 有值；`kill -9` 主进程后 3 秒内被 systemd 拉起