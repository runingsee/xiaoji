#!/usr/bin/env bash
# 晓记 · 一键部署（腾讯云控制台网页终端执行，无需 SSH）
#
# 用法：
#   bash deploy.sh [github仓库地址] [分支]
#   bash deploy.sh git@github.com:runingsee/xiaoji.git master
#
# 环境事实（依据 doc/部署上云指南.md、doc/部署经验总结.md 的 MiniLims 实测经验）：
#   - 服务器 118.89.200.74，2C2G，80 端口已被既有 .NET 服务占用 → 晓记走 8080
#   - 系统用户是 ubuntu（非 root），/opt 无写权限 → 一律用 ~/xiaoji
#   - 国内直连 github.com 的 https 被墙 → 必须 SSH-over-443（ssh.github.com:443）
#   - MySQL 是宿主机上的 mysql8 Docker 容器，xiaoji 库已建好
#   - 管理通道仅云控制台网页终端（VNC/OrcaTerm），SSH 已移除
set -euo pipefail

REPO_URL="${1:-git@github.com:runingsee/xiaoji.git}"   # 默认即本仓库
BRANCH="${2:-master}"
HOME_DIR="${XIAOJI_HOME:-$HOME/xiaoji}"
SRC_DIR="$HOME_DIR/src"
APP_DIR="$HOME_DIR/app"
CONFIG="${XIAOJI_CONFIG:-$HOME_DIR/appsettings.Production.json}"
LISTEN_PORT="${XIAOJI_PORT:-8080}"
BACKUP_KEEP=3

log()  { echo "[deploy] $*"; }
fail() { echo "[deploy] 错误：$*" >&2; exit 1; }

# 自举：dotnet 装在 ~/.dotnet（无 root 安装），非交互 shell 不读 .bashrc，
# 这里显式把 ~/.dotnet 补进 PATH / DOTNET_ROOT，避免 command -v dotnet 直接失败
if ! command -v dotnet >/dev/null 2>&1 && [ -x "$HOME/.dotnet/dotnet" ]; then
  export PATH="$HOME/.dotnet:$PATH"
  export DOTNET_ROOT="$HOME/.dotnet"
  log "已自动补环境变量：PATH 含 ~/.dotnet，DOTNET_ROOT=$DOTNET_ROOT"
fi

# ================================================================ 前置检查
log "=== 前置检查 ==="

command -v dotnet >/dev/null 2>&1 || fail "未找到 dotnet，请先安装 .NET SDK"
command -v git    >/dev/null 2>&1 || fail "未找到 git，执行：sudo apt install -y git"
command -v node   >/dev/null 2>&1 || fail "未找到 node，请先安装 Node.js 20+"

# 内存：2C2G 机器上 .NET publish + vite build 同时跑容易吃紧
# （MiniLims 踩过 JavaScript heap OOM，Vben 工程大；晓记工程小，风险低但仍需兜底）
MEM_MB=$(free -m | awk '/^Mem:/{print $2}')
SWAP_MB=$(free -m | awk '/^Swap:/{print $2}')
log "物理内存 ${MEM_MB}MB / swap ${SWAP_MB}MB"
if [ "$MEM_MB" -lt 3072 ] && [ "$SWAP_MB" -lt 1024 ]; then
  log "内存偏小且无 swap。构建中途若 OOM，先加 4G swap 再重跑："
  log "  sudo fallocate -l 4G /swapfile && sudo chmod 600 /swapfile"
  log "  sudo mkswap /swapfile && sudo swapon /swapfile"
  log "  echo '/swapfile none swap sw 0 0' | sudo tee -a /etc/fstab"
fi

# 运行时版本必须与 SDK 大版本一致，否则 publish 产物启动即失败
SDK_MAJOR=$(dotnet --version | cut -d. -f1)
if dotnet --list-runtimes | grep -q "Microsoft.NETCore.App ${SDK_MAJOR}\."; then
  log "SDK ${SDK_MAJOR}.x 与运行时版本匹配"
else
  log "警告：未检测到 .NET ${SDK_MAJOR}.x 运行时（MiniLims 踩过：只装 SDK 装运行时，"
  log "      容器反复 Restarting(139)）。请执行 dotnet --list-runtimes 确认。"
  log "      缺失则装：sudo apt install -y dotnet-runtime-${SDK_MAJOR}"
fi

# 数据库连通性预检：MySQL 在宿主机 mysql8 容器里，宿主 3306 应已做端口映射
log "检查 MySQL 连通性（127.0.0.1:3306）"
if (echo > /dev/tcp/127.0.0.1/3306) 2>/dev/null; then
  log "127.0.0.1:3306 可连"
else
  log "127.0.0.1:3306 连不上。若 MySQL 在 docker 容器中，检查端口映射："
  log "  docker ps | grep mysql8    # 需有 0.0.0.0:3306->3306/tcp"
  log "  宿主机未映射时，晓记应连容器 IP，或给容器补端口映射后 restart"
  log "（不阻断，部署后改配置重启即可）"
fi

# GitHub 连通性：https 被墙是已知问题
log "检查 GitHub 连通性"
if git ls-remote --exit-code "$REPO_URL" >/dev/null 2>&1; then
  log "仓库可访问"
else
  fail "仓库不可访问：$REPO_URL
  国内直连 github.com 的 https(443) 被墙，必须配置 SSH-over-443：
    ssh-keygen -t ed25519 -C 'xiaoji-server' -f ~/.ssh/id_ed25519_github -N ''
    cat ~/.ssh/id_ed25519_github.pub     # 粘贴到 GitHub → Settings → SSH keys
    cat >> ~/.ssh/config <<'EOF'
    Host github.com
        HostName ssh.github.com
        Port 443
        User git
        IdentityFile ~/.ssh/id_ed25519_github
        IdentitiesOnly yes
    EOF
    chmod 600 ~/.ssh/config
    ssh -T git@github.com                 # 期望回显 Hi <账号>!"
fi

[ -f "$CONFIG" ] || fail "缺少 $CONFIG（真实密钥，永不进 git）。
  创建方式：cp <仓库>/server/appsettings.Example.json $CONFIG 后填入真实值。
  连接串用 Server=127.0.0.1；账号用专用低权限账号，不要 root。"

# ================================================================ 拉代码
log "=== 拉取代码 ==="
mkdir -p "$HOME_DIR"
if [ -d "$SRC_DIR/.git" ]; then
  git -C "$SRC_DIR" fetch --depth 1 origin "$BRANCH"
  git -C "$SRC_DIR" reset --hard "origin/$BRANCH"
else
  git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$SRC_DIR"
fi

# ================================================================ 构建
log "=== 构建（首次约 3-6 分钟）==="

log "发布后端"
rm -rf "$SRC_DIR/server/publish"
# 部署机与服务同架构时用 framework-dependent（体积小、构建快）。
# 若架构不一致或运行时缺失，改用 self-contained：
#   dotnet publish ... -r linux-x64 --self-contained true
dotnet publish "$SRC_DIR/server/Huamishu.Api.csproj" \
  -c Release -o "$SRC_DIR/server/publish" --nologo

log "构建前端（限制 Node 堆内存，防 2G 机器 OOM）"
cd "$SRC_DIR/web"
[ -d node_modules ] || npm ci --no-audit --no-fund
NODE_OPTIONS=--max-old-space-size=1536 npm run build

# 静态资源并入后端 wwwroot，由 .NET 统一托管（SPA 路由回退已内置于 Program.cs）
rm -rf "$SRC_DIR/server/publish/wwwroot"
cp -r "$SRC_DIR/web/dist" "$SRC_DIR/server/publish/wwwroot"

# ================================================================ 替换
log "=== 替换运行版本 ==="
systemctl --user stop xiaoji 2>/dev/null || true

if [ -d "$APP_DIR" ]; then
  STAMP=$(date +%F-%H%M%S)
  log "备份现有版本 → $APP_DIR.bak-$STAMP"
  ls -1dt "$HOME_DIR"/app.bak-* 2>/dev/null | tail -n +$((BACKUP_KEEP + 1)) | xargs -r rm -rf
  mv "$APP_DIR" "$APP_DIR.bak-$STAMP"
fi
mkdir -p "$APP_DIR"
cp -r "$SRC_DIR/server/publish/." "$APP_DIR/"
cp -f "$CONFIG" "$APP_DIR/appsettings.Production.json"
chmod +x "$APP_DIR/Huamishu.Api" 2>/dev/null || true
chmod 600 "$APP_DIR/appsettings.Production.json"

# ================================================================ systemd
# 不用 /opt：ubuntu 对 /opt 无写权限（MiniLims 踩过 Permission denied）
# 关键：--urls 显式传 0.0.0.0。Kestrel 只绑回环会导致外部 502/Empty reply，
#      这是 MiniLims 排查最久的坑（doc/部署经验总结.md §6），此处显式规避。
cat > "$HOME_DIR/xiaoji.service" <<UNIT
[Unit]
Description=Xiaoji (晓记) web service
After=network.target docker.service
Wants=docker.service

[Service]
Type=simple
WorkingDirectory=$APP_DIR
# dotnet 装于 ~/.dotnet（无 root 安装），systemd 进程不读 .bashrc，必须显式注入
Environment=PATH=$HOME/.dotnet:/usr/local/sbin:/usr/local/bin:/usr/sbin:/usr/bin:/sbin:/bin
Environment=DOTNET_ROOT=$HOME/.dotnet
ExecStart=$APP_DIR/Huamishu.Api --urls http://0.0.0.0:$LISTEN_PORT --environment Production
Restart=always
RestartSec=3
# 2C2G 机器上限制单进程内存，防泄漏拖垮整机（MySQL 容器也要留够）
MemoryMax=600M
Environment=DOTNET_EnableDiagnostics=0
NoNewPrivileges=true
PrivateTmp=true

[Install]
WantedBy=default.target
UNIT

# ----------------------------------------------------------------
# 用户级 systemd 总线引导
# 云控制台网页终端（VNC/OrcaTerm）不是标准登录会话：
#   - 没有 XDG_RUNTIME_DIR / DBUS_SESSION_BUS_ADDRESS
#   - systemctl --user 报 Failed to connect to bus: No such file or directory
# 解法：先 loginctl enable-linger（创建 /run/user/<uid> 用户管理器与 bus 套接字），
#       再显式补环境变量，顺序不能反（之前 linger 排在 systemctl 之后导致本错误）。
# ----------------------------------------------------------------
log "启用 linger 并引导用户级 systemd 总线"
# loginctl enable-linger：系统启动时即拉起用户管理器，无需登录
if sudo loginctl enable-linger "$(whoami)" 2>/dev/null; then
  log "linger 已启用（无需登录，服务随系统启动）"
else
  log "linger 启用失败，请手动执行：sudo loginctl enable-linger $(whoami)"
fi
export XDG_RUNTIME_DIR="/run/user/$(id -u)"
export DBUS_SESSION_BUS_ADDRESS="unix:path=${XDG_RUNTIME_DIR}/bus"
mkdir -p "$XDG_RUNTIME_DIR" && chmod 700 "$XDG_RUNTIME_DIR" 2>/dev/null || true
for i in $(seq 1 10); do
  [ -S "${XDG_RUNTIME_DIR}/bus" ] && { log "用户总线就绪：${XDG_RUNTIME_DIR}/bus"; break; }
  sleep 1
done
[ -S "${XDG_RUNTIME_DIR}/bus" ] || log "警告：10 秒内未等到 ${XDG_RUNTIME_DIR}/bus，systemctl --user 可能仍不可用"

log "安装 systemd unit（用户级，无需 root）"
mkdir -p ~/.config/systemd/user
cp "$HOME_DIR/xiaoji.service" ~/.config/systemd/user/xiaoji.service
systemctl --user daemon-reload
systemctl --user enable xiaoji >/dev/null 2>&1 || true

systemctl --user restart xiaoji

# ================================================================ 健康检查
log "=== 健康检查（最长 40 秒）==="
for i in $(seq 1 40); do
  sleep 1
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$LISTEN_PORT/api/entries/summary" 2>/dev/null || echo 000)
  if [ "$CODE" = "401" ] || [ "$CODE" = "200" ]; then
    log "就绪：/api/entries/summary 返回 $CODE（401 = 服务活着且鉴权链路正常）"

    # 确认 Kestrel 绑了 0.0.0.0 而非 127.0.0.1（MiniLims §6 教训）
    LISTEN_HEX=$(printf '%X' "$LISTEN_PORT")
    if grep -qi "00000000:${LISTEN_HEX}" /proc/net/tcp /proc/net/tcp6 2>/dev/null; then
      log "Kestrel 绑定 0.0.0.0:$LISTEN_PORT ✅（外部可访问）"
    else
      log "警告：/proc/net/tcp 未见 0.0.0.0:${LISTEN_HEX}，可能只绑了回环。"
      log "      外部会 502 / Empty reply。核对 systemd ExecStart 的 --urls 参数。"
    fi

    echo
    echo "  访问地址：http://118.89.200.74:$LISTEN_PORT"
    echo "  腾讯云安全组需放行入站 TCP $LISTEN_PORT，否则外网无法访问"
    echo "  运维：systemctl --user status xiaoji | restart xiaoji"
    echo "  日志：journalctl --user -u xiaoji -f --lines 50"
    exit 0
  fi
done

log "40 秒内未就绪，最后 30 行日志："
journalctl --user -u xiaoji --lines 30 --no-pager 2>/dev/null || true
exit 1
