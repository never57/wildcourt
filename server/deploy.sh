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
NGINX_CONF="$HOST_CODE_ROOT/nginx/nginx.conf"
ACTIVE_FILE="$HOST_CODE_ROOT/.deploy-active"
NGINX_CONTAINER="${NGINX_CONTAINER:-wildcourt-nginx}"

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

# ---- 3. 构建 standby 镜像（active 不受影响） ----
echo "==> 构建 api-${STANDBY} 镜像"
docker compose -f "$COMPOSE_FILE" build $NO_CACHE "api-${STANDBY}"

# ---- 4. 启动 standby 容器（--no-deps 避免连带动 mysql/redis） ----
echo "==> 启动 api-${STANDBY}"
docker compose -f "$COMPOSE_FILE" up -d --no-deps "api-${STANDBY}"

# ---- 5. 等待健康检查通过（最多 120 秒），失败则回滚 ----
echo "==> 等待 api-${STANDBY} 健康检查通过"
HEALTHY=0
STATUS="none"
for i in $(seq 1 60); do
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
  echo "!! api-${STANDBY} 健康检查未通过（status=${STATUS}），回滚：停止新容器，保留 ${ACTIVE} 继续服务"
  docker compose -f "$COMPOSE_FILE" stop "api-${STANDBY}" || true
  exit 1
fi
echo "==> api-${STANDBY} 健康检查通过"

# ---- 6. 切换 nginx 流量（改 bind mount 配置 → 校验 → reload） ----
echo "==> 切换 nginx 反代目标：api-${ACTIVE} → api-${STANDBY}"
sed -i "s|http://api-${ACTIVE}:5000|http://api-${STANDBY}:5000|g" "$NGINX_CONF"

if ! docker exec "$NGINX_CONTAINER" nginx -t >/dev/null 2>&1; then
  echo "!! nginx 配置校验失败，回滚配置并停止新容器，保留 ${ACTIVE} 继续服务"
  sed -i "s|http://api-${STANDBY}:5000|http://api-${ACTIVE}:5000|g" "$NGINX_CONF"
  docker compose -f "$COMPOSE_FILE" stop "api-${STANDBY}" || true
  exit 1
fi
docker exec "$NGINX_CONTAINER" nginx -s reload

# ---- 7. 停掉旧容器（优雅停机，在途请求处理完再退出） ----
echo "==> 停止旧容器 api-${ACTIVE}"
docker compose -f "$COMPOSE_FILE" stop "api-${ACTIVE}" || true

# ---- 8. 记录当前 active ----
echo "${STANDBY}" > "$ACTIVE_FILE"
echo "==== 蓝绿部署完成：active=${STANDBY} ===="
