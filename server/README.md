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

### 本地开发环境（Windows 免安装版）

- **.NET SDK**：已安装到 `C:\Users\StarDream\.dotnet`（10.0.302），把该目录加入 PATH 后可直接用 `dotnet`。
- **一键开关**：`scripts/start_server.bat`（启动 MySQL + 服务端，Ctrl+C 停止服务端）/ `scripts/stop_server.bat`（关闭服务端 + MySQL）。
- **MySQL**：免安装版 8.0.29 位于 `C:\Users\StarDream\mysql-8.0.29-winx64`（数据目录 `my.ini` 指定），root 空密码，库 `wlidcourt_dev`。启动方式：
  ```powershell
  Start-Process "C:\Users\StarDream\mysql-8.0.29-winx64\bin\mysqld.exe" -ArgumentList "--defaults-file=C:\Users\StarDream\mysql-8.0.29-winx64\my.ini" -WindowStyle Hidden
  ```
- **启动服务端**（必须 Development 环境，连接串在 `config/appsettings.Development.json`）：
  ```powershell
  cd server
  $env:ASPNETCORE_ENVIRONMENT = "Development"
  dotnet run --project src/WlidCourt.Api
  ```
  首次启动自动执行 EF Core 迁移建表（`Migrations/InitUsers`）。
- **登录协议**（与客户端 `AuthService` 对齐）：
  - 注册：`POST /api/v1/user/register` `{ username, password }`（password 为客户端 SHA256 hex，服务端 BCrypt 存储）
  - 登录：`POST /api/v1/user/login` → `{ token, userId }`；会话暂存内存 `SessionStore`（Redis 就绪后迁移）
  - 鉴权：除 register/login 外，请求头 `Authorization: Bearer {token}`
- **数据库变更**：改实体后执行 `dotnet ef migrations add <Name> --project src/WlidCourt.Data --startup-project src/WlidCourt.Api`，重启服务端自动应用。

## 测试

```bash
dotnet test WlidCourt.sln
```

## 数据库

表结构与 Redis Key 清单见《docs/design/15-项目目录结构.md》§15.3；初始化脚本 `scripts/init_db.sql`（骨架）。
