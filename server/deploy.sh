#!/usr/bin/env bash
#
# WildCourt API 蓝绿部署脚本
# 流程：构建 standby 镜像 → 启动 standby 容器 → 健康检查通过 →
#        修改 nginx 反代目标（nginx/nginx.conf）并 nginx -s reload（毫秒级）→ 停旧容器。
# 全程 active 容器持续承接流量，仅切换瞬间有毫秒级抖动；构建期间线上不受影响。
#
# 用法：
#   ./deploy.sh            # 常规发布（默认 active=blue）
#   ./deploy.sh --no-cache # 强制全新构建镜像
#
# 环境：
#   HOST_CODE_ROOT / COMPOSE_FILE 可覆盖（默认 /opt/wildcourt/server、docker-compose.yml）
#   可在宿主机或 API 容器内执行（容器内需挂载 docker.sock + 代码目录，DeployService 已具备）
#
# 注意：
#   - 依赖 GNU sed（Linux）；macOS 需 gsed。
#   - 首次从旧单容器架构迁移时，先删除遗留容器：docker rm -f wildcourt-api
#   - 两个容器共用 MySQL/Redis，standby 启动时的 EF Migrate 为幂等操作，不影响 active。
set -euo pipefail

HOST_CODE_ROOT="${HOST_CODE_ROOT:-/opt/wildcourt/server}"
COMPOSE_FILE="${COMPOSE_FILE:-$HOST_CODE_ROOT/docker-compose.yml}"
# nginx.conf 是运行时文件（被 .gitignore 忽略，不入库），由本脚本从模板生成
NGINX_TEMPLATE="$HOST_CODE_ROOT/nginx/nginx.conf.template"
NGINX_CONF="$HOST_CODE_ROOT/nginx/nginx.conf"
ACTIVE_FILE="$HOST_CODE_ROOT/.deploy-active"
NGINX_CONTAINER="${NGINX_CONTAINER:-wildcourt-nginx}"

# ---- 0. 日志：每次部署独立文件（带时间戳），避免被外部 tee 覆盖 ----
# 之前用 /tmp/deploy-last.log 会被下次部署覆盖，失败后无法追溯；
# 现在写到 logs/deploy-YYYYMMDD-HHMMSS.log，保留历史便于排查。
LOG_DIR="$HOST_CODE_ROOT/logs"
mkdir -p "$LOG_DIR"
LOG_FILE="$LOG_DIR/deploy-$(date +%Y%m%d-%H%M%S).log"
# 同时输出到终端和日志文件（>log 2>&1 + tee 让 stdout/stderr 都进文件，终端也能实时看）
exec > >(tee "$LOG_FILE") 2>&1
echo "==== deploy.sh 开始：$(date '+%F %T') 日志文件：$LOG_FILE ===="

# 从模板生成 nginx.conf（用 sed | tee 原地写回，保持 inode，bind mount 立即对容器生效）
gen_nginx_conf() {
  local target="$1"   # 目标：api-blue / api-green
  sed "s|http://api-blue:5000|http://${target}:5000|g" "$NGINX_TEMPLATE" | tee "$NGINX_CONF" >/dev/null
}

NO_CACHE=""
if [ "${1:-}" = "--no-cache" ]; then
  NO_CACHE="--no-cache"
fi

cd "$HOST_CODE_ROOT"

# ---- 1. 安全检查：检测旧单容器架构遗留 ----
if docker inspect wildcourt-api >/dev/null 2>&1; then
  echo "!! 检测到旧架构容器 wildcourt-api（单实例），请先手动清理后重试："
  echo "   docker rm -f wildcourt-api"
  exit 1
fi

# ---- 2. 确定 active / standby（默认 active=blue） ----
ACTIVE="$(cat "$ACTIVE_FILE" 2>/dev/null || true)"
if [ "$ACTIVE" != "blue" ] && [ "$ACTIVE" != "green" ]; then
  ACTIVE="blue"
fi
if [ "$ACTIVE" = "blue" ]; then
  STANDBY="green"
else
  STANDBY="blue"
fi
echo "==== 蓝绿部署开始：active=${ACTIVE}，standby=${STANDBY} ===="

# 确保 nginx.conf 存在（bind mount 源文件，缺失会导致 docker 把挂载点建为目录使 nginx 启动失败）
if [ ! -f "$NGINX_CONF" ]; then
  echo "==> nginx.conf 不存在，从模板生成（指向 api-${ACTIVE}）"
  gen_nginx_conf "api-${ACTIVE}"
fi

# ---- 3. 构建 standby 镜像（active 不受影响） ----
echo "==> 构建 api-${STANDBY} 镜像"
docker compose -f "$COMPOSE_FILE" build $NO_CACHE "api-${STANDBY}"

# ---- 4. 启动 standby 容器（--no-deps 避免连带动 mysql/redis） ----
# 关键：先移除旧的 standby 容器再 create + start，避免 `docker compose up -d` 因配置变化
# 走 recreate（先停旧容器再启动新容器）时被中断，留下"新容器 Created 未启动 + 旧容器已停"的中间态。
# 蓝绿部署下 standby 本就不承接流量，直接 rm 旧 standby 无影响。
echo "==> 启动 api-${STANDBY}"
docker rm -f "wildcourt-api-${STANDBY}" 2>/dev/null || true
docker compose -f "$COMPOSE_FILE" up -d --no-deps --force-recreate "api-${STANDBY}"

# ---- 5. 等待健康检查通过（最多 180 秒），失败则回滚 ----
# 窗口设计：compose healthcheck start_period=60s + retries=20×interval=5s ≈ 160s 才会判 unhealthy；
# 这里给 180s 留余量，避免 deploy.sh 先于 docker healthcheck 判定失败而误回滚。
# DB 相关部署（含迁移）容器启动会跑 Migrate，首次/大表迁移可能 20-30s+，必须留够窗口。
echo "==> 等待 api-${STANDBY} 健康检查通过（最多 180 秒，迁移期间耐心等待）"
HEALTHY=0
STATUS="none"
for i in $(seq 1 90); do
  STATUS="$(docker inspect --format '{{.State.Health.Status}}' "wildcourt-api-${STANDBY}" 2>/dev/null || echo 'none')"
  if [ "$STATUS" = "healthy" ]; then
    HEALTHY=1
    break
  fi
  if [ "$STATUS" = "unhealthy" ]; then
    break
  fi
  sleep 2
done

if [ "$HEALTHY" != "1" ]; then
  echo "!! api-${STANDBY} 健康检查未通过（status=${STATUS}），准备回滚"
  # ---- 回滚保护：先确认 active 容器还在运行，避免停 standby 后两边全空 → 502 ----
  # 历史教训：若前一次部署失败/中断导致 active 已停，这次回滚停 standby 后会无上游，
  # nginx 502。这里显式检查 active 状态，缺失则告警但不自动 start（无法判断镜像版本）。
  ACTIVE_RUNNING="$(docker inspect --format '{{.State.Running}}' "wildcourt-api-${ACTIVE}" 2>/dev/null || echo 'false')"
  if [ "$ACTIVE_RUNNING" != "true" ]; then
    echo "!!! 严重：active 容器 api-${ACTIVE} 未运行（前次部署可能已中断/失败）"
    echo "!!! 停 standby 后将无可用上游 → nginx 必然 502。中止回滚，保留 standby 供排查："
    echo "    docker logs --tail 100 wildcourt-api-${STANDBY}"
    echo "    docker start wildcourt-api-${ACTIVE}  # 手动恢复旧 active 后重试部署"
    exit 1
  fi
  echo "   active 容器 api-${ACTIVE} 仍在运行，安全回滚：停 standby，保留 active 继续服务"
  docker compose -f "$COMPOSE_FILE" stop "api-${STANDBY}" || true
  exit 1
fi
echo "==> api-${STANDBY} 健康检查通过"

# ---- 5.5 前置自检：nginx 必须挂载宿主配置，否则蓝绿切换静默失效 ----
# 容器内 /etc/nginx/conf.d/default.conf 应与宿主 NGINX_CONF 完全一致（bind mount）。
# 若不一致（如容器缺挂载、读到镜像内置旧配置），reload 只会作用于镜像内配置，
# 切换永不生效；这里自动重建容器修复。
echo "==> 自检 nginx bind mount（${NGINX_CONTAINER} ↔ ${NGINX_CONF}）"
if ! docker exec "$NGINX_CONTAINER" cat /etc/nginx/conf.d/default.conf 2>/dev/null | diff -q - "$NGINX_CONF" >/dev/null 2>&1; then
  echo "!! nginx 容器未加载宿主 nginx.conf（bind mount 未生效），蓝绿切换无法生效，自动重建 nginx 容器"
  docker compose -f "$COMPOSE_FILE" up -d --force-recreate --no-deps nginx
  if ! docker exec "$NGINX_CONTAINER" cat /etc/nginx/conf.d/default.conf 2>/dev/null | diff -q - "$NGINX_CONF" >/dev/null 2>&1; then
    echo "!! nginx 容器重建后仍未挂载宿主配置，中止部署；请检查 docker-compose.yml 中 nginx 服务的 volumes 配置"
    exit 1
  fi
  echo "==> nginx 容器已重建，bind mount 生效"
fi

# ---- 6. 切换 nginx 流量（从模板生成目标配置 → 校验 → reload） ----
# 注意：生成时必须保持 inode（用 tee 原地写回），不能用 sed -i / 新建文件改名，
# 否则 bind mount 的 nginx 容器会继续读到旧 inode 的配置（inode 陷阱），切换"看似成功实则失效"。
echo "==> 切换 nginx 反代目标：api-${ACTIVE} → api-${STANDBY}"
gen_nginx_conf "api-${STANDBY}"

if ! docker exec "$NGINX_CONTAINER" nginx -t >/dev/null 2>&1; then
  echo "!! nginx 配置校验失败，回滚配置并停止新容器，保留 ${ACTIVE} 继续服务"
  gen_nginx_conf "api-${ACTIVE}"
  docker compose -f "$COMPOSE_FILE" stop "api-${STANDBY}" || true
  exit 1
fi
docker exec "$NGINX_CONTAINER" nginx -s reload

# ---- 6.5 切换后自检 ----
# ① bind mount inode 兜底：确认容器内读到的配置确实指向 standby；
#    若仍指向 active（inode 陷阱），自动重建 nginx 容器让挂载绑定新 inode。
echo "==> 切换后自检：nginx 容器内配置已指向 api-${STANDBY}"
if ! docker exec "$NGINX_CONTAINER" cat /etc/nginx/conf.d/default.conf 2>/dev/null | grep -q "http://api-${STANDBY}:5000"; then
  echo "!! nginx 容器内仍是旧配置（bind mount inode 陷阱），自动重建 nginx 容器"
  docker compose -f "$COMPOSE_FILE" up -d --force-recreate --no-deps nginx
  sleep 1
  if ! docker exec "$NGINX_CONTAINER" cat /etc/nginx/conf.d/default.conf 2>/dev/null | grep -q "http://api-${STANDBY}:5000"; then
    echo "!! nginx 容器重建后仍未指向 api-${STANDBY}，中止部署；请检查 nginx 服务 volumes 配置"
    exit 1
  fi
  echo "==> nginx 容器已重建，配置生效"
fi
# ② 实测反代连通性（此时 standby 与 active 均在运行）
echo "==> 切换后自检：nginx 反代 api-${STANDBY} 连通性"
sleep 1
HTTP_CODE="$(docker exec "$NGINX_CONTAINER" curl -s -o /dev/null -w '%{http_code}' http://127.0.0.1/api/v1/admin/auth/captcha 2>/dev/null || echo '000')"
if [ "$HTTP_CODE" != "200" ]; then
  echo "!! 切换后自检失败：HTTP ${HTTP_CODE}（期望 200），nginx 容器内当前配置/日志："
  docker exec "$NGINX_CONTAINER" nginx -T 2>&1 | grep -E 'proxy_pass|emerg' || true
  docker logs --tail 5 "$NGINX_CONTAINER" 2>&1 || true
  echo "   请排查 nginx 挂载与容器网络；此时 active 与 standby 容器均在运行，修复后重跑部署即可"
  exit 1
fi
echo "==> 切换后自检通过（HTTP 200）"

# ---- 7. 停掉旧容器（优雅停机，在途请求处理完再退出） ----
echo "==> 停止旧容器 api-${ACTIVE}"
docker compose -f "$COMPOSE_FILE" stop "api-${ACTIVE}" || true

# ---- 8. 记录当前 active ----
echo "${STANDBY}" > "$ACTIVE_FILE"
echo "==== 蓝绿部署完成：active=${STANDBY} ===="
