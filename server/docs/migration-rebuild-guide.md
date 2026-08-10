# 迁移重做与重新部署指引（snake_case 列名 + EF9 保持）

> ✅ **已于 2026-08-04 执行完成**：删库重建、新迁移 InitSchema 已生效、Redis 会话验证通过（重启 api 不掉线）。
> 以下为操作记录，供后续参考。

> 背景：登录闭环修复包含 **列名统一 snake_case**（Pomelo.EntityFrameworkCore.MySql 无 10.x
> 稳定版，EF 保持 9.0.18/Pomelo 9.0.0），旧的 `20260803092406_InitUsers` 迁移
> （EF9 + PascalCase 列名）与新模型不兼容，需删库重建。
> 数据库当前只有测试号（无需保留），重建成本极低。

## 1. 为什么必须重做

- 旧迁移由 EF Core 9.0.18 生成，模型快照记录的是 **PascalCase 列名**（UserId/Username/...）
- 新 DbContext Fluent 全部改为 **snake_case**（user_id/username/...），旧迁移快照与新模型不一致
- 旧迁移快照记录的是 PascalCase 列名，新 DbContext Fluent 已全部改为 snake_case，二者不匹配（需删旧迁移重新生成）
- 服务器数据库已按旧迁移建表 → **需删库重建**（仅 1 个测试号，无风险）

## 2. 本地：重做迁移（Windows，需 .NET 10 SDK）

```powershell
cd server

# 确保 dotnet-ef 工具可用（若未安装）
dotnet tool install --global dotnet-ef
# 或更新到最新：dotnet tool update --global dotnet-ef

# 删除旧迁移（EF9 + PascalCase）
Remove-Item -Recurse -Force src/WildCourt.Data/Migrations

# 重新生成迁移（snake_case 列名）
dotnet ef migrations add InitSchema `
    --project src/WildCourt.Data `
    --startup-project src/WildCourt.Api

# 构建 + 跑单测（登录闭环测试应全绿）
dotnet build
dotnet test
```

> 确认生成的迁移列名为 snake_case（`user_id`/`username`/`created_at`...）后再继续。

## 3. 服务器：删库重建 + 重新部署

```bash
# 3.1 停服 + 删数据卷（重建库；测试号无需保留，重建后重新注册即可）
docker compose down
docker volume ls | grep mysql_data        # 查实际卷名（通常 wildcourt_mysql_data）
docker volume rm wildcourt_mysql_data

# 3.2 上传新代码并重建
#   （本地把更新后的 server/ 上传到 /opt/wildcourt/server，含新迁移、csproj、源码）
bash cloud_deploy.sh                      # 构建镜像 + 启动 + 自动 Migrate（新表结构）
```

## 4. 验证

```bash
docker compose ps                         # 三服务 Up
curl http://localhost:5000/api/v1/user/register -X POST -H "Content-Type: application/json" -d '{}'
# 预期 {"code":400,"message":"密码不能为空"} → 服务正常

# 注册/登录走通后，确认 Redis 会话生效（重启 api 不掉线）：
docker compose restart api
# 再次用旧 token 调 /api/v1/user/profile → 应仍返回 200 + 用户信息（Redis 会话持久）
```

## 5. 测试期公网直连（可选）

compose 已将 api 端口收口为 `127.0.0.1:5000:5000`（HTTPS 前置）。
测试期如需公网直连：把该行改回 `"5000:5000"`，并在腾讯云轻量防火墙放行 5000。
正式上线：保持 `127.0.0.1`，走 Nginx + HTTPS（模板见 deployment-issues.md §三）。
