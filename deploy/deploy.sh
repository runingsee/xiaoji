#!/usr/bin/env bash
# 晓记 · 一键部署（腾讯云控制台网页终端执行，无需 SSH）
#
# 用法：
#   bash deploy.sh <github仓库地址> [分支]
# 例：
#   bash deploy.sh https://github.com/<你的账号>/xiaoji.git master
#
# 前提：服务器已装 .NET SDK 与 git；数据库 xiaoji 库已建好。
set -euo pipefail

REPO_URL="${1:-}"
BRANCH="${2:-master}"
APP_DIR="/opt/xiaoji/app"
SRC_DIR="/opt/xiaoji/src"
CONFIG="/opt/xiaoji/appsettings.Production.json"
LISTEN_PORT="${XIAOJI_PORT:-8080}"
BACKUP_KEEP=3

log() { echo "[deploy] $*"; }
fail() { echo "[deploy] 错误：$*" >&2; exit 1; }

# ---------------------------------------------------------------- 前置检查
[ -n "$REPO_URL" ] || fail "用法：bash deploy.sh <github仓库地址> [分支]"

command -v dotnet >/dev/null 2>&1 || fail "未找到 dotnet，请先安装 .NET SDK"
command -v git    >/dev/null 2>&1 || fail "未找到 git，请先安装 git"
git ls-remote --exit-code "$REPO_URL" >/dev/null 2>&1 \
  || fail "仓库不可访问（私有仓库请配置凭据，见脚本末尾说明）：$REPO_URL"

# 运行时版本必须与 SDK 一致，否则 publish 出来的产物启动即失败
SDK_MAJOR=$(dotnet --version | cut -d. -f1)
RUNTIME_DIR="/usr/share/dotnet/shared/Microsoft.NETCore.App"
if [ -d "$RUNTIME_DIR" ]; then
  HAS_RUNTIME=$(ls "$RUNTIME_DIR" 2>/dev/null | grep -c "^${SDK_MAJOR}\." || true)
  [ "$HAS_RUNTIME" -gt 0 ] || fail "只装了 SDK ${SDK_MAJOR}.x，缺少对应运行时，请执行：dotnet --list-runtimes 确认"
fi

[ -f "$CONFIG" ] || fail "缺少 $CONFIG（真实密钥，永不进 git）。可从 server/appsettings.Example.json 复制后填写"

# ---------------------------------------------------------------- 拉代码
log "拉取代码：$REPO_URL ($BRANCH)"
mkdir -p "$SRC_DIR"
if [ -d "$SRC_DIR/.git" ]; then
  git -C "$SRC_DIR" fetch --depth 1 origin "$BRANCH"
  git -C "$SRC_DIR" reset --hard "origin/$BRANCH"
else
  git clone --depth 1 --branch "$BRANCH" "$REPO_URL" "$SRC_DIR"
fi

# ---------------------------------------------------------------- 构建
log "发布后端（self-contained，无需服务器运行时）"
rm -rf "$SRC_DIR/server/publish"
dotnet publish "$SRC_DIR/server/Huamishu.Api.csproj" \
  -c Release -o "$SRC_DIR/server/publish" --nologo

log "构建前端"
cd "$SRC_DIR/web"
[ -d node_modules ] || npm ci --no-audit --no-fund
npm run build

# 静态资源并入后端 wwwroot，由 .NET 统一托管（SPA 路由回退已内置）
rm -rf "$SRC_DIR/server/publish/wwwroot"
cp -r "$SRC_DIR/web/dist" "$SRC_DIR/server/publish/wwwroot"

# ---------------------------------------------------------------- 备份旧版
if [ -d "$APP_DIR" ]; then
  STAMP=$(date +%F-%H%M%S)
  log "备份现有版本 → $APP_DIR.bak-$STAMP"
  rm -rf "$APP_DIR.bak-"* 2>/dev/null || true
  ls -1dt "$APP_DIR".bak-* 2>/dev/null | tail -n +$((BACKUP_KEEP + 1)) | xargs -r rm -rf
  mv "$APP_DIR" "$APP_DIR.bak-$STAMP"
fi
mkdir -p "$APP_DIR"
cp -r "$SRC_DIR/server/publish/." "$APP_DIR/"
chmod +x "$APP_DIR/Huamishu.Api" 2>/dev/null || true
log "配置文件位置：$CONFIG（请确认已复制到发布目录）"
cp -f "$CONFIG" "$APP_DIR/appsettings.Production.json"

# 专用低权限账号运行
id -u xiaoji >/dev/null 2>&1 || useradd -r -s /usr/sbin/nologin xiaoji
chown -R xiaoji:xiaoji "$APP_DIR"
chmod 640 "$APP_DIR/appsettings.Production.json"

# ---------------------------------------------------------------- 重启服务
cat > /etc/systemd/system/xiaoji.service <<UNIT
[Unit]
Description=Xiaoji (晓记) web service
After=network.target mysql.service
Wants=mysql.service

[Service]
Type=simple
User=xiaoji
Group=xiaoji
WorkingDirectory=$APP_DIR
ExecStart=$APP_DIR/Huamishu.Api --urls http://0.0.0.0:$LISTEN_PORT --environment Production
Restart=always
RestartSec=3
MemoryMax=700M
Environment=DOTNET_EnableDiagnostics=0
NoNewPrivileges=true
PrivateTmp=true
ProtectSystem=full

[Install]
WantedBy=multi-user.target
UNIT

log "重启 systemd"
systemctl daemon-reload
systemctl enable xiaoji >/dev/null 2>&1 || true
systemctl restart xiaoji

# ---------------------------------------------------------------- 健康检查
log "等待服务就绪（最长 40 秒）"
for i in $(seq 1 40); do
  sleep 1
  CODE=$(curl -s -o /dev/null -w '%{http_code}' "http://127.0.0.1:$LISTEN_PORT/api/entries/summary" 2>/dev/null || echo 000)
  # 401 = 服务活着且鉴权链路正常，即视为就绪
  if [ "$CODE" = "401" ] || [ "$CODE" = "200" ]; then
    log "就绪：http://<服务器IP>:$LISTEN_PORT  （/api/entries/summary 返回 $CODE）"
    echo
    echo "  访问地址：http://$(curl -s ifconfig.me 2>/dev/null || echo '<服务器IP>'):$LISTEN_PORT"
    echo "  腾讯云安全组需放行入站 TCP $LISTEN_PORT，否则外网无法访问"
    echo "  服务管理：systemctl status xiaoji / systemctl restart xiaoji"
    echo "  查看日志：journalctl -u xiaoji -f --lines 50"
    exit 0
  fi
done

log "40 秒内未就绪，最后 30 行日志："
journalctl -u xiaoji --lines 30 --no-pager || true
exit 1

# ---------------------------------------------------------------- 私有仓库说明
# 若仓库为私有，二选一：
#   1) 部署机上放 deploy key（推荐，不留 Token）：
#        ssh-keygen -t ed25519 -C "xiaoji-deploy" -N "" -f ~/.ssh/xiaoji_deploy
#        cat ~/.ssh/xiaoji_deploy.pub   # 粘贴到 GitHub 仓库 Settings → Deploy keys
#        git config --global url."ssh://git@github.com/<账号>/<仓库>.git".insteadOf "https://github.com/<账号>/<仓库>.git"
#   2) 或用 PAT（注意：会留在服务器上）：
#        git config --global credential.helper store
#        git clone https://<PAT>@github.com/<账号>/<仓库>.git $SRC_DIR
