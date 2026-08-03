# 腾讯云部署方案（从本地迁移）

> 依据：`docs/design/17-开发环境与运维.md`（成本估算）、`config/appsettings.Production.json`、`Program.cs` 配置目录机制。
> 当前状态：服务端为骨架阶段（业务未实现、不依赖 MySQL/Redis）；本文为迁移到腾讯云的完整步骤清单。

## 0. 本地现状（已完成）

- .NET 10.0.302 SDK 已安装到 `C:\Users\StarDream\.dotnet`（用户目录，无需管理员权限）。
- `server/WlidCourt.sln` restore + build 通过（0 错误 0 警告）。
- 服务可启动并监听 `http://localhost:5000`（Development/Production 均可，骨架阶段不连库）。

本地日常启动：

```bash
# Windows 终端（把 .NET 加进 PATH 后可省略完整路径）
cd server
set PATH=%USERPROFILE%\.dotnet;%PATH%
dotnet run --project src/WlidCourt.Api
# 或指定环境：set ASPNETCORE_ENVIRONMENT=Development
```

## 1. 服务器选型（腾讯云）

| 阶段 | 规格 | 参考月费 | 说明 |
| --- | --- | --- | --- |
| 测试 / 几百人在线 | 轻量应用服务器 2核4G | 60~90 元 | 快速起步，自带防火墙/快照 |
| 正式上线 / 几千人在线 | CVM 4核8G | 200~300 元 | 文档 17.3 建议 |

- 系统：**Ubuntu 22.04/24.04 LTS**（.NET 10 官方支持；`appsettings.Production.json` 的 `/opt/wlidcourt` 路径即 Linux 惯例）。
- 建议在控制台把系统时区设为 UTC+8（每日结算 Job 04:00 触发）。

## 2. 中间件（MySQL / Redis）

业务实现后 `Program.cs` 目标态会校验 MySQL/Redis 连通性并快速失败，需先就绪：

| 组件 | 推荐 | 备选 |
| --- | --- | --- |
| MySQL 8.0 | 腾讯云数据库 TencentDB for MySQL（内网地址，免运维） | 服务器自装 `apt install mysql-server` |
| Redis | 腾讯云 Redis | 服务器自装 `apt install redis-server` |

- 建库：执行 `server/scripts/init_db.sql`（当前为骨架，正式表结构以 EF Core 迁移为准）。
- 注意：TencentDB 默认不允许外网直连，应用走内网 IP。

## 3. 发布与上传

```bash
# 本机（Windows）交叉发布到 linux-x64
cd server
dotnet publish src/WlidCourt.Api -c Release -r linux-x64 --self-contained false -o ./publish
# 或 --self-contained true（服务器免装 .NET，体积更大）

# 上传
scp -r publish/* user@<服务器IP>:/opt/wlidcourt/
```

- 数值配置：`client/Assets/StreamingAssets/config/` 下的 JSON（`Config_PlayerLevel.json`、`Config_YardUnlock.json` 等）同步到 `/opt/wlidcourt/config/`。
- 运行依赖 .NET：服务器上安装 SDK 或运行时：`apt install dotnet-sdk-10.0`（微软源）或使用自包含发布。

## 4. 配置

- 编辑 `config/appsettings.Production.json`：
  - `ConnectionStrings.MySql`：填腾讯云数据库内网连接串
  - `Redis.ConnectionString`：填腾讯云 Redis
  - `Aliyun.AccessKeyId/Secret`：内容审核密钥（继续用阿里云 API 可跨厂商调用；若改用腾讯云内容安全需改 `Utils/AliyunAudit.cs`）
- 部署时设置环境变量：
  - `ASPNETCORE_ENVIRONMENT=Production`
  - `WLIDCOURT_CONFIG_DIR=/opt/wlidcourt/config`（Program.cs 已支持覆盖配置目录）

## 5. 进程托管（systemd）

`/etc/systemd/system/wlidcourt.service`：

```ini
[Unit]
Description=WildCourt Server
After=network.target

[Service]
WorkingDirectory=/opt/wlidcourt
ExecStart=/usr/bin/dotnet /opt/wlidcourt/WlidCourt.Api.dll
Environment=ASPNETCORE_ENVIRONMENT=Production
Environment=WLIDCOURT_CONFIG_DIR=/opt/wlidcourt/config
Restart=always
RestartSec=5

[Install]
WantedBy=multi-user.target
```

```bash
systemctl daemon-reload && systemctl enable --now wlidcourt
```

## 6. 对外暴露（Nginx + HTTPS）

- 安全组/防火墙：只开放 80/443（及 SSH 22）。
- Nginx 反向代理：

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
    }
}
```

- 客户端 `GameManager` 的服务端地址从 `localhost:5000` 改为 `https://api.你的域名.com`（文档 17.4 联调步骤 8）。

## 7. 资源与成本（文档 17.3 汇总）

| 项目 | 月费 | 用途 |
| --- | --- | --- |
| 云服务器 2核4G | 60~90 | 应用 |
| COS 对象存储 | 50~200 | 热更 AB 包/资源 |
| CDN | 50~150 | 资源分发加速 |
| 内容审核（AI） | 100~500 | 信件审核 |
| 域名 + SSL | 10 | 均摊 |
| **合计** | **270~1350** | 视用户规模 |

## 8. 上线前检查清单

- [ ] `dotnet test` 全绿（业务实现后）
- [ ] MySQL/Redis 连通性校验通过（Program.cs 目标态步骤 5）
- [ ] `init_db.sql` / EF Core 迁移已执行
- [ ] 数值配置 JSON 已同步到 `/opt/wlidcourt/config/`
- [ ] 时区 UTC+8（每日结算 Job）
- [ ] Nginx WebSocket 升级头（SignalR ChatHub）
- [ ] 安全组最小化开放端口
- [ ] 备份策略（`server/scripts/backup_db.sh` 当前为骨架，需实现）
