# 部署检查与上线待办（腾讯云 Docker 方案）

> 更新：2026-08-04（**重建部署已完成**：删库重建、snake_case 新表、Redis 会话已生效）
> 关联：`docs/deploy-to-tencent-cloud.md`（部署步骤）、`docs/REVIEW-ISSUES.md`（登录闭环代码审查）、`docs/migration-rebuild-guide.md`（迁移重做指引）

## 一、部署现状 ✅（已确认无误）

| 项 | 状态 | 说明 |
| --- | --- | --- |
| `.dockerignore` | ✅ | 排除 `**/bin/` `**/obj/` `.env` `docs/` `.git/`，构建上下文干净、`.env` 不泄漏进镜像 |
| 容器时区 | ✅ | api 与 mysql 均 `TZ: Asia/Shanghai`（每日结算 Job 04:00 UTC+8 边界正确） |
| 连接串覆盖 | ✅ | `AddEnvironmentVariables()` + compose 环境变量 `ConnectionStrings__MySql` / `Redis__ConnectionString` 覆盖 Production 占位值 |
| 服务名匹配 | ✅ | `Server=mysql`、`redis:6379` 与 compose 服务名一致 |
| 端口隔离 | ✅ | MySQL(3306)/Redis(6379) 仅绑定 `127.0.0.1`；api 当前 `5000:5000` 测试期公网直连（云防火墙放行 5000），正式上线收口 `127.0.0.1:5000:5000` 走 Nginx+HTTPS |
| 启动顺序 | ✅ | `depends_on: service_healthy` 等 MySQL 就绪 → api 启动 → `Migrate()`（失败降级启动，不再崩溃循环） |
| 配置挂载 | ✅ | `./config:/app/config:ro` + `WILDCOURT_CONFIG_DIR=/app/config` |
| 密钥 | ✅ | Production 配置全 `CHANGE_ME` 占位，真实值经 `.env` 注入，不入库不镜像 |

## 二、上线前必须处理 ⚠️

| # | 事项 | 状态 | 说明 |
| --- | --- | --- | --- |
| 1 | **HTTPS** | ⏳ 待办 | compose 已收口 `127.0.0.1:5000`（当前测试期为 `5000:5000` 公网直连），需：域名解析 + 腾讯云免费 SSL + Nginx 反代（模板见下），防火墙只留 80/443/22 |
| 2 | **Redis 会话落地** | ✅ 已部署生效 | `RedisUtil` 已实现，`SessionStore` 切 Redis（`ISessionStore` 接口）；2026-08-04 重建部署后生效，重启 api 不掉线 |
| 3 | **数据库连接兜底** | ✅ 已部署生效 | `Migrate()` 失败仅告警降级启动（Program.cs）；2026-08-04 重建后验证通过 |

## 三、Nginx + HTTPS 配置模板（上线 #1 用）

```nginx
server {
    listen 443 ssl;
    server_name api.你的域名.com;
    ssl_certificate     /etc/nginx/ssl/fullchain.pem;   # 腾讯云免费证书
    ssl_certificate_key /etc/nginx/ssl/privkey.pem;

    location / {
        proxy_pass http://127.0.0.1:5000;
        proxy_http_version 1.1;
        proxy_set_header Upgrade $http_upgrade;        # SignalR WebSocket 必需
        proxy_set_header Connection "upgrade";
        proxy_set_header Host $host;
        proxy_set_header X-Real-IP $remote_addr;
    }
}
server {
    listen 80;
    server_name api.你的域名.com;
    return 301 https://$host$request_uri;
}
```

## 四、代码遗留（登录闭环，详见 REVIEW-ISSUES.md）

- `ChatHub` 未实现（占位）；实现时在 Hub 内鉴权（取 `context.Items["UserId"]`）
- 其余 6 张表实体未补齐
- 四个 Service 占位测试待业务实现后补全

## 五、运维命令备忘

```bash
docker compose ps              # 状态
docker compose logs -f api     # 服务端日志
docker compose logs mysql      # 数据库日志
docker compose restart api     # 重启 api
bash cloud_deploy.sh           # 一键部署/更新（会校验 .env 占位密码）
```
