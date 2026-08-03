#!/usr/bin/env bash
# ============================================================
# WildCourt 云端一键部署（在服务器上执行：bash cloud_deploy.sh）
# 前提：server/ 目录已上传到服务器（含 Dockerfile / docker-compose.yml / config/）
# ============================================================
set -euo pipefail
cd "$(dirname "$0")/.."

echo "==> [1/4] 检查 .env 配置"
if [ ! -f .env ]; then
    cp .env.example .env
    echo "首次运行：已生成 .env，请编辑设置数据库密码后重新执行"
    echo "    vim .env    # 修改 MYSQL_ROOT_PASSWORD / MYSQL_APP_PASSWORD"
    exit 1
fi
if grep -q "change_me" .env; then
    echo "错误：.env 里还有 change_me 占位密码，请先修改：vim .env"
    exit 1
fi

echo "==> [2/4] 启动容器（MySQL + Redis + API）"
docker compose up -d --build

echo "==> [3/4] 等待服务就绪（最长 60 秒）"
for i in $(seq 1 30); do
    if curl -sf -o /dev/null -X POST -H "Content-Type: application/json" -d '{}' \
        http://localhost:5000/api/v1/user/register; then
        echo "==> [4/4] 部署成功 ✅"
        docker compose ps
        echo ""
        echo "验证：curl http://localhost:5000/api/v1/user/register -X POST -H \"Content-Type: application/json\" -d '{}'"
        echo "     （预期返回 {\"code\":400,...}）"
        exit 0
    fi
    sleep 2
done

echo "服务未就绪，请排查："
echo "    docker compose logs api"
echo "    docker compose logs mysql"
exit 1
