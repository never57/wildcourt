# WildCourt 服务端

.NET 10 Web API 服务端，依据《docs/architecture/02-服务端架构.md》与《docs/design/15-项目目录结构.md》搭建（骨架阶段，业务逻辑待实现）。

## 项目结构

```
server/
├── WlidCourt.sln
├── src/
│   ├── WlidCourt.Api/      # Web API 宿主：Controllers / Services / Jobs / Hubs / Middleware / Utils / Program.cs
│   ├── WlidCourt.Data/     # EF Core DbContext / Entities / Migrations（MySQL）
│   └── WlidCourt.Tests/    # xUnit 测试（匹配 / 结算 / 好友 / 庭院）
├── config/                 # appsettings.{Environment}.json
├── scripts/                # 部署 / 备份 / 热更 / 建库脚本（骨架）
└── docs/                   # api.yaml / database_erd.png（骨架）
```

## 配置

- 数值配置：与客户端共用 `StreamingAssets/config/Config_*.json`（JSON 格式，见《docs/design/16-数值配置表规范.md》），服务端启动时加载供 `PlayerService` / `MatchService` 等使用。
- 应用配置：`config/appsettings.{Environment}.json`（通过 `ASPNETCORE_ENVIRONMENT` 切换）。

## 构建与运行

```bash
dotnet restore WlidCourt.sln
dotnet build WlidCourt.sln
dotnet run --project src/WlidCourt.Api
```

## 测试

```bash
dotnet test WlidCourt.sln
```

## 数据库

表结构与 Redis Key 清单见《docs/design/15-项目目录结构.md》§15.3；初始化脚本 `scripts/init_db.sql`（骨架）。
