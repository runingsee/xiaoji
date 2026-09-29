# 晓记 · 云服务器部署手册（腾讯云 118.89.200.74）

> 复用 MiniLims 的部署经验（见 `doc/部署上云指南.md`、`doc/部署经验总结.md`），
> 晓记的差异是：**单体 .NET 同时托管前端与 API**（无 Docker、不用 Nginx），端口走 8080 避开已占用的 80。

## 0. 环境事实（已实测确认）

| 项 | 值 | 来源 |
| --- | --- | --- |
| 服务器 IP | **118.89.200.74** | MiniLims 文档；实测 80 返回 nginx/1.31.6 + ZRAdmin.NET |
| 配置 | 2C2G（构建吃紧） | MiniLims 文档 |
| 系统用户 | `ubuntu`（非 root，**`/opt` 无写权限**） | MiniLims 踩坑记录 |
| 管理通道 | 云控制台网页终端（VNC/OrcaTerm），**SSH 已移除**（曾被入侵） | MiniLims 文档 |
| 80 端口 | 已被既有 .NET 服务占用，**不可动** | 实测 |
| MySQL | 宿主机上的 `mysql8` **Docker 容器**，3306 已做端口映射；`xiaoji` 库已建好 | 实测连通 + 冒烟测试通过 |
| Node / .NET SDK | 已装 | 用户确认 |
| 域名 / HTTPS | **无**。只能 `http://118.89.200.74:8080` 明文访问 | 用户确认 |
| GitHub 通道 | **https 被墙**，必须 SSH-over-443（`ssh.github.com:443`） | MiniLims 实测 `Empty reply from server` |

> 注：早前提过的 `18.89.200.74` 少了个开头的 `1`，实测全端口无响应。**以 `118.89.200.74` 为准。**

### 与 MiniLims 的架构差异

```
MiniLims:  web(nginx :80) ──/prod-api──► api(.NET :8888) ──host.docker.internal──► mysql8 容器
晓记:      .NET 监听 0.0.0.0:8080（同托管前端静态资源 + API）──127.0.0.1:3306──► mysql8 容器
```

晓记不用 Docker、不用 Nginx 的原因：

- 前端产物直接并入后端 `wwwroot`，`Program.cs` 已内置 SPA 回退，少一层反代少一个故障点
- 80 端口的既有站点不能动，绕开 Nginx 减少耦合
- 无域名时 Nginx 的 HTTPS 价值为零
- 限流已由应用层 `strict` 策略承担（10 次 / 10 秒 / IP）

## 1. 腾讯云安全组

| 端口 | 动作 | 说明 |
| --- | --- | --- |
| **8080** | **放行入站** 0.0.0.0/0 | 晓记唯一对外口。个人工具；若只自己用，来源填固定公网 IP |
| 80 | 保持现状 | 既有 ZRAdmin 服务 |
| 3306 | **关闭对外** | 晓记在服务器本地用 127.0.0.1 连；`mini_lims` 若仍需远程访问，来源限为你的固定 IP |
| 6379 | **关闭对外** | 同上 |
| 22 | 保持关闭 | SSH 已移除，管理走控制台 |

## 2. MySQL 账号与库

当前 `appsettings.json` 用的是 `root` 连公网 IP，这是全链路最该修的一处。

```bash
# 1) 专用低权限账号（xiaoji 应用只需增删改查）
docker exec mysql8 mysql -uroot -p -e "
CREATE USER IF NOT EXISTS 'xiaoji'@'%' IDENTIFIED BY '强密码';
GRANT SELECT, INSERT, UPDATE, DELETE ON xiaoji.* TO 'xiaoji'@'%';
FLUSH PRIVILEGES;"

# 2) 确认 xiaoji 库已存在
docker exec mysql8 mysql -uroot -p -e "SHOW DATABASES LIKE 'xiaoji';"
# 若不存在，本地执行（schema.sql 在仓库里）：
#   type server\Data\schema.sql | docker exec -i mysql8 mysql -uroot -p
```

> **注意**：MySQL 在 Docker 容器里，**没有** `systemctl restart mysql`、也没有 `/etc/mysql/mysql.conf.d/mysqld.cnf`。
> 改配置要靠 `docker exec` 或重建容器。旧文档里那种"编辑 mysqld.cnf 再 systemctl restart"的做法在这台机器上不成立。

> MiniLims 经验 §4：老容器与新版 Docker 的网络转发不兼容，表现是「容器 Up 但服务不可达」。
> 若连不上，先 `docker ps` 确认 mysql8 状态，再
> `docker exec mysql8 mysql -uroot -p -e "SELECT 1;"` 验证；
> 仍失败才考虑重建容器——**重建前务必确认原 `docker run` 参数含数据卷挂载**（MiniLims 挂在 `/opt/mysql/data`），
> 否则 `mini_lims` 数据会丢。

> 本机开发仍需连 `118.89.200.74:3306` 时，安全组把 3306 来源限制为你的固定公网 IP，不要 0.0.0.0/0。

## 3. 本地：推送代码到 GitHub

仓库已配置，无需重复添加：

```bash
git remote -v   # origin  git@github.com:runingsee/xiaoji.git
git push -u origin master
```

推送前必查（MiniLims 踩过 `.gitignore` 误伤构建依赖文件的坑）：

```bash
git ls-files | grep appsettings      # 只应出现 .Development.json 和 .Example.json
git ls-files | grep package-lock     # 必须存在，否则服务器 npm ci 失败
```

## 4. 服务器：配置 GitHub 通道

服务器 `ubuntu` 用户需要**单独一把**密钥（不是开发机上那把）。

```bash
# 1) 生成密钥
ssh-keygen -t ed25519 -C "xiaoji-server" -f ~/.ssh/id_ed25519_github -N ""
cat ~/.ssh/id_ed25519_github.pub

# 2) 公钥粘贴到 GitHub → Settings → SSH and GPG keys → New SSH key → Authentication key

# 3) SSH-over-443（关键：国内直连 github.com 的 443 https 被墙）
cat >> ~/.ssh/config <<'EOF'
Host github.com
    HostName ssh.github.com
    Port 443
    User git
    IdentityFile ~/.ssh/id_ed25519_github
    IdentitiesOnly yes
EOF
chmod 600 ~/.ssh/config

# 4) 验证，期望回显 "Hi runingsee!"
ssh -T git@github.com
```

## 5. 服务器：首次拉代码 + 填真实配置

**必须先 clone，配置文件模板和 deploy.sh 都在仓库里**（顺序不能反）：

```bash
# 1) clone 到 deploy.sh 约定的路径
git clone --depth 1 -b master git@github.com:runingsee/xiaoji.git ~/xiaoji/src

# 2) 复制模板（此时 src 已存在）
cp ~/xiaoji/src/server/appsettings.Example.json ~/xiaoji/appsettings.Production.json
nano ~/xiaoji/appsettings.Production.json
```

填入真实值：

```json
{
  "ConnectionStrings": {
    "MySql": "Server=127.0.0.1;Port=3306;Database=xiaoji;User=xiaoji;Password=真实密码;"
  },
  "Jwt": {
    "Secret": "openssl rand -base64 48 的输出",
    "Issuer": "xiaoji",
    "Audience": "xiaoji-web"
  },
  "DeepSeek": {
    "ApiKey": "sk-真实Key",
    "BaseUrl": "https://api.deepseek.com",
    "Model": "deepseek-chat"
  }
}
```

```bash
chmod 600 ~/xiaoji/appsettings.Production.json
```

## 6. 部署

```bash
# deploy.sh 在 clone 下来的仓库里，不要 cd 到 /tmp 找
bash ~/xiaoji/src/deploy/deploy.sh
# 等价写法（仓库地址可省略，脚本默认即本仓库）
bash ~/xiaoji/src/deploy/deploy.sh git@github.com:runingsee/xiaoji.git master
```

`deploy/deploy.sh` 依次做：

1. **前置检查**：dotnet / git / node 存在；SDK 与运行时大版本匹配；内存与 swap；
   `127.0.0.1:3306` 可连；GitHub 仓库可访问（不通则打印 SSH-over-443 配置步骤）
2. **拉代码**：`~/xiaoji/src` 已存在 `.git` 则 `fetch + reset --hard`，否则 `git clone --depth 1`
3. **构建**：`dotnet publish -c Release` → `npm ci`（首次）+ `NODE_OPTIONS=--max-old-space-size=1536 npm run build`
4. **合并产物**：`web/dist` 复制进 `publish/wwwroot`
5. **替换**：旧版本备份到 `~/xiaoji/app.bak-<时间戳>`，保留 3 份
6. **systemd**：写**用户级** unit 到 `~/.config/systemd/user/xiaoji.service`（无需 root），
   `Restart=always`、`MemoryMax=600M`、`NoNewPrivileges`
7. **linger**：`sudo loginctl enable-linger ubuntu`，保证未登录时服务随系统启动
8. **健康检查**：轮询 `/api/entries/summary`，401 即就绪；再查 `/proc/net/tcp`
   确认 Kestrel 绑了 `0.0.0.0` 而非回环

> 脚本用 `--urls http://0.0.0.0:8080` 显式指定监听。MiniLims 在这里踩过最大的坑
> （`doc/部署经验总结.md` §6）：配置文件里的 `localhost:8888` 覆盖了环境变量，
> Kestrel 只绑回环，外部表现为 nginx 502 + `Empty reply from server`，排查耗时很久。
> 晓记的 `Program.cs` 无 `urls` 配置项，监听只由命令行决定，结构上不会犯这个错，
> 脚本仍会做一次 `/proc/net/tcp` 校验兜底。

## 7. 日常更新

```bash
# 本地
git add . && git commit -m "..." && git push

# 服务器
bash ~/xiaoji/src/deploy/deploy.sh
```

`node_modules` 已存在时跳过 `npm ci`，重复部署约 1-2 分钟。

## 8. 日常运维

**注意是用户级 systemd**，命令都带 `--user`：

| 操作 | 命令 |
| --- | --- |
| 状态 | `systemctl --user status xiaoji` |
| 重启 | `systemctl --user restart xiaoji` |
| 日志 | `journalctl --user -u xiaoji -f --lines 50` |
| 排错 | `journalctl --user -u xiaoji --since "1 hour ago" \| grep -iE "error\|exception\|fail"` |
| 健康检查 | `curl -s -o /dev/null -w '%{http_code}\n' http://127.0.0.1:8080/api/entries/summary`（期望 401） |
| 确认监听 | `grep -i 1F90 /proc/net/tcp`（8080 = 1F90，应见 `00000000:1F90`） |
| 回滚 | `mv ~/xiaoji/app.bak-<时间戳> ~/xiaoji/app && systemctl --user restart xiaoji` |
| 释放磁盘 | `rm -rf ~/xiaoji/app.bak-*` |

崩溃恢复由 `Restart=always` + `RestartSec=3` 负责。

## 9. 每日备份

```bash
# 密码独立放 env 文件，权限收紧
cat > ~/xiaoji/backup.env <<'EOF'
DB_USER=xiaoji
DB_PASS=真实密码
DB_NAME=xiaoji
EOF
chmod 600 ~/xiaoji/backup.env

cp ~/xiaoji/src/deploy/backup.sh ~/xiaoji/backup.sh
chmod +x ~/xiaoji/backup.sh
~/xiaoji/backup.sh        # 手动跑一次，确认有文件产出

crontab -e
# 每天 02:30
30 2 * * * /home/ubuntu/xiaoji/backup.sh
```

> `backup.sh` 直接调 `mysqldump`。若 `mysqldump` 不在 PATH（数据库在容器里时常见），
> 改用 `docker exec mysql8 mysqldump -uxiaoji -p"$DB_PASS" --single-transaction xiaoji | gzip > ...`，
> 其余逻辑（空文件校验、保留 7 份）不变。

恢复演练（每月一次）：

```bash
gunzip -c ~/xiaoji/backups/xiaoji-*.sql.gz | docker exec -i mysql8 mysql -uxiaoji -p xiaoji
```

**每周把 `~/xiaoji/backups/` 下载到本地**。备份留在服务器上，服务器整个挂掉时一起没了。
另可在腾讯云控制台对数据卷目录（`/opt/mysql/data`）做**磁盘快照**。

## 10. 内存兜底（2C2G 机器）

服务器上已有 MiniLims 容器 + MySQL，构建时内存容易触顶。若构建中途 OOM：

```bash
sudo fallocate -l 4G /swapfile
sudo chmod 600 /swapfile
sudo mkswap /swapfile
sudo swapon /swapfile
echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab
free -h
```

## 11. Nginx 说明

晓记**不经过 Nginx**，由 .NET 直接监听 `0.0.0.0:8080`。理由见 §0。

若日后买了域名想套 HTTPS，再在 Nginx 加一个 `listen 443` 的 server 块反代 `127.0.0.1:8080`，
同时把 systemd 里的 `--urls` 改回 `http://127.0.0.1:8080`。参考 `deploy/nginx.conf`。

## 12. 账号安全（待你决定，尚未处理）

控制台密码登录是当前最大风险敞口。攻击目标是腾讯云账号本身——拿到即可重置密码、创建密钥、随时进服务器。
MiniLims 已因端口暴露被入侵过一次（SSH 因此被移除）。建议至少做其一：

- 腾讯云账号绑定 MFA
- 恢复 SSH 密钥登录，22 端口只放行固定 IP，关闭密码登录
- 数据库按 §2 收敛到专用低权限账号，3306 不对公网

## 13. FAQ

| 现象 | 原因与处理 |
|---|---|
| `bash: deploy.sh: No such file or directory` | 还没 clone。先 `git clone ... ~/xiaoji/src`，脚本在 `~/xiaoji/src/deploy/deploy.sh` |
| `cp: cannot stat .../appsettings.Example.json` | 同上，模板在仓库里，必须先 clone |
| `git ls-remote` 失败 / `Empty reply from server` | https 被墙，按 §4 配 SSH-over-443 |
| `Permission denied` 写到 `/opt` | ubuntu 无 `/opt` 写权限，本项目一律用 `~/xiaoji` |
| `systemctl status xiaoji` 报 Unit not found | 忘了加 `--user`；unit 在 `~/.config/systemd/user/` |
| 服务反复 `Restarting(139)` | ① SDK 与运行时版本不匹配（`dotnet --list-runtimes`）；② MySQL 连不上（多为容器网络问题，见 §2） |
| 外部访问 502 / `Empty reply from server` | Kestrel 只绑了回环。`grep -i 1F90 /proc/net/tcp` 应见 `00000000:1F90` 而非 `0100007F:1F90` |
| 外部完全连不上 | 腾讯云安全组未放行 8080 入站 |
| `npm ci` 报缺文件 | 确认 `web/package-lock.json` 已入库（`git ls-files \| grep package-lock`） |
| 构建时 `JavaScript heap out of memory` | 按 §10 加 swap；晓记前端工程小，正常不会触发 |
| 部署完 401 是正常吗 | 是。`/api/entries/summary` 需要 JWT，未带 token 返回 401 即说明服务与鉴权链路正常 |
| 改了配置要重新部署吗 | 不用。改 `~/xiaoji/appsettings.Production.json` 后 `systemctl --user restart xiaoji` |
| 8080 会被占用吗 | 部署前 `ss -lntp \| grep 8080` 确认；换端口用 `XIAOJI_PORT=8090 bash ~/xiaoji/src/deploy/deploy.sh` |

## 14. 部署自检清单

- [ ] `git ls-files | grep appsettings` 无真实配置文件
- [ ] `git ls-files | grep package-lock` 有输出
- [ ] 服务器 `ssh -T git@github.com` 回显 `Hi runingsee!`
- [ ] `~/xiaoji/src` 已 clone（deploy.sh 和模板都在里面）
- [ ] MySQL 有 `xiaoji` 库 + `xiaoji` 低权限账号（§2）
- [ ] `~/xiaoji/appsettings.Production.json` 存在、权限 600、连接串为 `127.0.0.1` + 非 root
- [ ] 安全组放行 8080；3306/6379 未对公网
- [ ] `bash ~/xiaoji/src/deploy/deploy.sh` 输出「就绪」+「Kestrel 绑定 0.0.0.0」
- [ ] `curl http://127.0.0.1:8080/api/entries/summary` 返回 401
- [ ] 外网访问 `http://118.89.200.74:8080/` 能打开登录页
- [ ] 移动端浏览器可注册登录并口述记账
- [ ] `kill -9` 主进程后 3 秒内自动拉起
- [ ] 备份 cron 生效，`~/xiaoji/backups/` 有当日文件
- [ ] `loginctl show-user ubuntu | grep Linger` 为 `Linger=yes`
