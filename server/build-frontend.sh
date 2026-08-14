#!/usr/bin/env bash
# WildCourt 管理后台前端构建脚本
# 用法：bash build-frontend.sh [--no-cache]
# 作用：在服务器上 npm run build 生成 admin-web/dist，nginx 通过 bind mount 即时读取，
#       前端更新无需重建 nginx 镜像/容器（零中断、无 502）。
#
# 前提：服务器已安装 Node 20+；依赖已通过 npm ci 安装（首次运行自动执行）。

set -euo pipefail

HOST_CODE_ROOT="${HOST_CODE_ROOT:-/opt/wildcourt/server}"
FRONTEND_DIR="$HOST_CODE_ROOT/admin-web"

echo "==== 前端构建开始：$(date '+%F %T') ===="
cd "$FRONTEND_DIR"

# 可选：国内镜像加速
NPM_REGISTRY="${NPM_REGISTRY:-https://registry.npmjs.org}"

# 依赖安装（node_modules 不存在或 package.json 变化时）
if [ ! -d "node_modules" ]; then
    echo "==> 安装前端依赖（npm ci）"
    npm ci --registry="$NPM_REGISTRY"
fi

echo "==> 构建前端产物（npm run build）"
npm run build

echo "==> 前端构建完成，产物：$FRONTEND_DIR/dist"
echo "==> nginx 通过 bind mount 即时读取，无需重启容器。"
