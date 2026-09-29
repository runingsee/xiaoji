# 晓记 · 部署手册（腾讯云轻量服务器 / Linux）

> 当前实际环境：无域名、80 端口已被既有 .NET 服务（ZRAdmin）占用、仅腾讯云控制台网页终端可操作、无 SSH。
> 因此本手册的主线是 **GitHub 拉源码 + 服务器本机构建 + 8080 端口直出**。

## 环境事实

| 项 | 值 |
| --- | --- |
| 服务器 IP | 见下方「待确认」一节 |
| 操作系统 | Ubuntu / Debian（systemd + apt） |
| 已有服务 | 80 端口由 Nginx 反代既有 .NET 应用（ZRAdmin），勿动 |
| 晓记端口 | **8080**（腾讯云安全组需放行 TCP 8080 入站） |
| 已有工具 | .NET SDK、git、Nginx、MySQL 8 |
| 域名 / HTTPS | 无。只能 `http://<IP>:8080` 明文访问 |

## 0. 待确认：服务器 IP

用户提供过两个地址，实测结果不一致，**部署前必须确认**：

| IP | 80 端口 | 3306 端口 | 结论 |
| --- | --- | --- | --- |
| `18.89.200.74` | 无响应 | 无响应 | 端口全关，部署完也访问不了 |
| `118.89.200.74` | 开着，nginx/1.31.6，ZRAdmin.NET | 开着 | 与「80 已有 .NET 服务」吻合，应为真实机器 |

`server/appsettings.json` 里的数据库连接串写的是 `118.89.200.74`，与上表第二行一致。

## 1. 前置准备

### 1.1 腾讯云安全组

放行一条入站规则：

- 协议 TCP
- 端口 **8080**
- 来源 0.0.0.0/0（个人工具；若只自己用，可填你家固定公网 IP）

**不要**放行 3306、5000。5000 只在服务器本地回环使用，不应对外。

### 1.2 MySQL 加固（重要）

当前 `appsettings.json` 用的是 `root` 账号且连公网 IP，这是最该修的一处。

```bash
# 1) 专用低权限账号
mysql -u root -p -e "
CREATE USER 'xiaoji'@'localhost' IDENTIFIED BY '强密码';
GRANT SELECT, INSERT, UPDATE, DELETE ON xiaoji.* TO 'xiaoji'@'localhost';
FLUSH PRIVILEGES;"

# 2) 只监听本地回环，编辑 /etc/mysql/mysql.conf.d/mysqld.cnf
#    [mysqld] 下确认 bind-address = 127.0.0.1
#    2G 内存机器顺手调优：
#    innodb_buffer_pool_size = 256M
#    innodb_log_file_size = 64M
#    max_connections = 100
#    performance_schema = OFF

systemctl restart mysql

# 3) 验证只监听本地
ss -lntp | grep 3306
```

预期只看到 `127.0.0.1:3306`。改完后从公网 `telnet <IP> 3306` 应连不通。

### 1.3 配置文件（真实密钥，永不进 git）

```bash
mkdir -p /opt/xiaoji
cp <仓库路径>/server/appsettings.Example.json /opt/xiaoji/appsettings.Production.json
nano /opt/xiaoji/appsettings.Production.json
```

关键项填真实值，其余保持：

```json
"ConnectionStrings": {
  "MySql": "Server=127.0.0.1;Port=3306;Database=xiaoji;User=xiaoji;Password=真实密码;"
},
"Jwt": { "Secret": "openssl rand -base64 48 的输出" },
"DeepSeek": { "ApiKey": "sk-真实Key" }
```

```bash
chmod 600 /opt/xiaoji/appsettings.Production.json
```

## 2. 建 GitHub 仓库并推送

```bash
# 开发机
cd <本地仓库>
git remote add origin https://github.com/<你的账号>/xiaoji.git
git push -u origin master
```

**必须确认 `server/appsettings.json` 没被推上去**：

```bash
git ls-files | grep appsettings     # 只应出现 .Development.json 和 .Example.json
```

私有仓库的服务器侧凭据配置见 `deploy.sh` 末尾注释，优先用 deploy key（不落 Token）。

## 3. 部署

服务器上（腾讯云控制台 → 登录 → 网页终端）：

```bash
# 首次需要装 git（若已装可跳过）
apt update && apt install -y git

# 部署
cd /tmp
# 把 deploy.sh 内容贴进终端（网页终端无法传文件，只能粘贴）
bash deploy.sh https://github.com/<你的账号>/xiaoji.git master
```

脚本做的事：

1. 校验 `dotnet` / `git` / SDK 与运行时大版本一致
2. `git clone --depth 1` 到 `/opt/xiaoji/src`
3. `dotnet publish -c Release`
4. `npm ci && npm run build`，产物复制进 `publish/wwwroot`（.NET 统一托管，SPA 回退已内置）
5. 旧版本备份到 `/opt/xiaoji/app.bak-<时间戳>`，保留 3 份
6. 写 `/etc/systemd/system/xiaoji.service`（`User=xiaoji` 低权限账号、`Restart=always`、`MemoryMax=700M`、`NoNewPrivileges`）
7. `systemctl restart` 后轮询 `/api/entries/summary`，返回 401/200 即视为就绪

私有仓库追加一步（deploy key 方式）：

```bash
ssh-keygen -t ed25519 -C "xiaoji-deploy" -N "" -f ~/.ssh/xiaoji_deploy
cat ~/.ssh/xiaoji_deploy.pub    # 粘贴到 GitHub 仓库 Settings → Deploy keys（只读勾选）
git config --global url."ssh://git@github.com/<账号>/xiaoji.git".insteadOf "https://github.com/<账号>/xiaoji.git"
```

## 4. 日常更新

代码推到 GitHub 后，服务器上只需重跑一条命令：

```bash
bash /tmp/deploy.sh https://github.com/<你的账号>/xiaoji.git master
```

建议把 `deploy.sh` 放在 `/opt/xiaoji/deploy.sh`，以后直接：

```bash
bash /opt/xiaoji/deploy.sh
```

脚本不重建前端以外的中间产物，`npm ci` 只在 `node_modules` 缺失时执行，重复部署较快。

## 5. 日常运维

```bash
systemctl status xiaoji              # 运行状态
systemctl restart xiaoji             # 重启
journalctl -u xiaoji -f --lines 50   # 实时日志
journalctl -u xiaoji --since "1 hour ago" | grep -i error   # 排错
curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/api/entries/summary   # 健康检查，期望 401
```

崩溃恢复由 systemd `Restart=always` 负责，`RestartSec=3` 保证 3 秒内拉起。

## 6. 每日备份

```bash
cat > /opt/xiaoji/backup.env <<'EOF'
DB_USER=xiaoji
DB_PASS=真实密码
DB_NAME=xiaoji
EOF
chmod 600 /opt/xiaoji/backup.env

cp /opt/xiaoji/src/deploy/backup.sh /opt/xiaoji/backup.sh
chmod +x /opt/xiaoji/backup.sh
/opt/xiaoji/backup.sh          # 手动跑一次验证能出文件

crontab -e
# 每天 02:30 执行：
30 2 * * * /opt/xiaoji/backup.sh
```

恢复演练（每月一次）：

```bash
gunzip -c /opt/xiaoji/backups/xiaoji-*.sql.gz | mysql -u xiaoji -p xiaoji
```

**每周把 `/opt/xiaoji/backups/` 下载一份到本地**——备份留在服务器上，服务器整个挂掉时一起没了。

## 7. Nginx 说明

晓记**不经过 Nginx**，由 .NET 直接监听 8080。理由：

- 无域名，Nginx 的 HTTPS 价值为零
- 80 端口的既有站点不能动，减少一处耦合
- 限流已由应用层 `strict` 策略承担（10 次 / 10 秒 / IP）

若日后买了域名想套 HTTPS，再在 Nginx 加一个 `listen 443` 的 server 块反代 `127.0.0.1:8080`，同时把 systemd 里的 `--urls` 改回 `http://127.0.0.1:8080`。参考 `deploy/nginx.conf`。

## 8. 账号安全（尚未处理，需你决定）

控制台密码登录是目前最大的风险敞口，攻击目标是腾讯云账号本身——拿到就能重置密码、创建密钥、随时进服务器。建议至少做其一：

- 腾讯云账号绑定 MFA
- 开 SSH 密钥登录，22 端口安全组只放行固定 IP，关闭密码登录
- 数据库已按 1.2 收敛到 127.0.0.1

## 自检清单

- [ ] 服务器 IP 已确认（18.89.200.74 还是 118.89.200.74）
- [ ] 安全组放行 TCP 8080；未放行 3306 / 5000
- [ ] `ss -lntp | grep 3306` 只显示 127.0.0.1
- [ ] MySQL 使用专用低权限账号，非 root
- [ ] `/opt/xiaoji/appsettings.Production.json` 存在且权限 600
- [ ] `git ls-files | grep appsettings` 无真实配置文件
- [ ] `curl http://127.0.0.1:8080/api/entries/summary` 返回 401
- [ ] 外网 `curl http://<IP>:8080/api/auth/login` 返回 401（非 404/500）
- [ ] `kill -9` 主进程后 3 秒内 systemd 拉起
- [ ] 备份 cron 已生效，`/opt/xiaoji/backups/` 有当日文件
