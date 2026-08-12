# WildCourt.Mcp — 管理后台 MCP Server

把 WildCourt 管理后台（部署 + 数据库）接入 CodeBuddy 的 MCP 桥接服务。

## 它做什么

启动一个 stdio 协议的 MCP Server（本地进程），通过 `/api/v1/admin/*` REST 接口
把管理后台的能力暴露为 MCP 工具，CodeBuddy 对话里即可直接查询/操作：

**部署工具**
| 工具 | 说明 |
| --- | --- |
| `deploy_status` | 查询部署状态（是否在构建、上次结果、代码目录） |
| `deploy_logs` | 查询最近部署日志（构建输出） |
| `deploy_build` | 触发部署：Service 为空或 `api` 系列走**蓝绿部署**（API 无中断、失败自动回滚，空 Service 还会重建 nginx 前端）；其他服务走 `docker compose up -d --build` |
| `deploy_list_files` | 列出服务端源码目录 |
| `deploy_read_file` | 读取服务端源码文件（2MB 内文本） |
| `deploy_save_file` | 在线保存/覆盖源码文件（需重新构建生效） |
| `deploy_mkdir` | 新建源码子目录 |
| `deploy_delete_file` | 删除源码文件/目录（不可恢复，谨慎） |

**数据库工具**
| 工具 | 说明 |
| --- | --- |
| `db_list_databases` | 列出业务数据库 |
| `db_list_tables` | 列出数据库下的表 |
| `db_table_data` | 分页读取表数据 |
| `db_execute_sql` | 执行 SQL（DDL/DML，高危） |
| `db_insert_row` | 新增一行 |
| `db_update_row` | 按主键更新一行 |
| `db_delete_row` | 按主键删除一行（不可恢复，谨慎） |

## 配置

MCP Server 用管理后台账号登录（密码客户端 SHA256 后传输，与服务端约定一致）。

三种配置方式（优先级从高到低）：

1. **环境变量**（推荐，适合 CodeBuddy 配置 MCP 时填写）
   - `WILDCOURT_ADMIN_URL` — 管理 API 地址，如 `http://your-server.example/api/v1/admin`
   - `WILDCOURT_ADMIN_USERNAME` — 管理员账号（默认 `admin`）
   - `WILDCOURT_ADMIN_PASSWORD` — 管理员明文密码（程序自动 SHA256）
2. **`appsettings.local.json`**（构建目录下，已 gitignore）
   ```json
   {
     "Admin": {
       "BaseUrl": "http://your-server.example/api/v1/admin",
       "Username": "admin",
       "Password": "你的密码"
     }
   }
   ```
3. **`appsettings.json`**（默认值，仅本地开发，密码是占位符）

## 构建

```bash
cd server
dotnet publish src/WildCourt.Mcp -c Release -o publish/WildCourt.Mcp
```

## 接入 CodeBuddy

1. 构建得到 `publish/WildCourt.Mcp/WildCourt.Mcp.exe`（Windows）或 `WildCourt.Mcp`（Linux）；
2. 在 CodeBuddy 的 MCP 配置中新增一个 **stdio** 类型 Server：
   - command: `dotnet`（或直接指向已发布的可执行文件）
   - args: `[publish 目录下的 dll 路径]`，如
     `C:\wildcourt\server\publish\WildCourt.Mcp\WildCourt.Mcp.dll`
   - env（可选，推荐）：
     - `WILDCOURT_ADMIN_URL=http://your-server.example/api/v1/admin`
     - `WILDCOURT_ADMIN_USERNAME=admin`
     - `WILDCOURT_ADMIN_PASSWORD=你的密码`
3. 保存后 CodeBuddy 即可发现 `deploy_*` / `db_*` 工具。

## 安全说明

- 仅本机运行，不暴露任何公网端口（stdio 由 CodeBuddy 拉起）；
- 危险操作（删除文件、删除行、执行 SQL）在工具描述中已标注，
  但**请确认你有权限执行这些操作，并注意生产环境数据安全**；
- 密码不会写入仓库（`appsettings.local.json` 已加入 `.gitignore`）。

## 权限说明

MCP 工具的操作权限与所配置的管理员账号一致——请使用**最小权限**账号，
避免用超管账号连接日常 MCP 会话。
