# 服务端代码审查报告（登录闭环）

> 审查时间：2026-08-03
> 范围：`server/` 登录闭环（账号+密码注册/登录/会话/鉴权），基于 review 子代理对未提交改动的审查。
> 结论：**核心闭环正确，可继续开发；上线前需修复 4 处中等问题。**

---

## 一、已验证 ✅（闭环正确）

- **密码链路闭环**：客户端 SHA256 一次哈希（`AuthService.HashPassword`，lowercase hex）→ 服务端 BCrypt 存储/校验（`UserService.cs:48,65`），明文不落服务端，无二次哈希。
- **业务码一致**：`ApiCodes`（Models/ApiCodes.cs）与客户端 `NetConst.cs` 逐项对齐。
- **会话合理**：`SessionStore` token 128 位、7 天 TTL、过期懒清理。
- **异常映射正确**：401/409/404 等业务码 → 对应 HTTP 状态 + `{ code, message }` 信封。
- **无硬编码密钥**：Production 配置全为 `CHANGE_ME` 占位。

## 二、Should-fix（中）—— 上线/联调前处理

| # | 位置 | 问题 | 建议 |
| --- | --- | --- | --- |
| 1 | `Middleware/AuthMiddleware.cs:24` | 只放行 login/register/根路径，**`/hub/chat` 的 SignalR negotiate 与 WebSocket Upgrade 全被 401 拦截**，且不解析 SignalR 惯例的 `?access_token=` | 放行 Hub 路径，或在中间件读取 query string 里的 token |
| 2 | `Program.cs` 启动段 | 启动即 `ServerVersion.AutoDetect` + `db.Database.Migrate()`，**MySQL 未就绪进程直接起不来**；与部署文档「骨架阶段不连库、本地可启动」矛盾 | 启动降级：连库失败仅告警，不退出；或部署文档同步更新 |
| 3 | `Utils/SessionStore.cs:10` + `RedisUtil.cs:8-28` | 会话目前是**内存实现，Redis 全部 NotImplemented**；部署文档却假设 Redis 会话就绪。多实例/重启会全员掉线 | 上线前落地 Redis 会话实现（StackExchange.Redis） |
| 4 | csproj 版本错配 | EF Core 9.0.18 / Pomelo 9.0.0 跑在 net10.0（Data csproj:12-17、Api csproj:16-22）。能编译但非官方支持组合 | 升级 EF Core 10.x + Pomelo 10.x |

## 三、低优先级 / 注意

| # | 位置 | 问题 |
| --- | --- | --- |
| 5 | `WlidCourt.Tests` | 登录闭环零测试：仅有 Friend/Match/Settlement/Yard 四个 Service 测试，`UserService`/`SessionStore`/`AuthMiddleware` 未覆盖 |
| 6 | `Services/UserService.cs:40-54` | 注册 `AnyAsync` + 唯一索引存在 TOCTOU：并发同名注册抛 `DbUpdateException` 变 500 而非 409 |
| 7 | `Models/User.cs:7` vs 客户端 `UserDtos.cs:31-41` | 客户端 `UserProfileResponse` 期望 shell/coupon/token，服务端 User 缺 shell/coupon（货币未实现的契约占位） |
| 8 | `Migrations/20260803092406_InitUsers.cs:109-131` | 列名 PascalCase（UserId/Username/CreatedAt），文档 15.3 约定 snake_case（user_id/username/created_at）——需统一（影响迁移 + SQL） |
| 9 | 迁移命名 | `InitUsers` 实际创建全部 7 张表（:18-148），命名与内容不符 |
| 10 | 迁移 `yards` 表（:133-148） | `UserId` 设 IdentityColumn 且全库无 FK；注册不创建默认 yard 行，依赖 `YardService` 兜底 |
| 11 | `SessionStore.cs:22` | 无滑动续期、重复登录产生多个并存 token（设计取舍，可接受） |

## 四、待办占位（非缺陷，按计划推进）

- `RedisUtil` 全部方法（Utils/RedisUtil.cs:8-28）
- `ChatHub` 两方法（Hubs/ChatHub.cs:12-20）
- `scripts/init_db.sql`：仅建库无建表（建表以 EF Core 迁移为准）
- `deploy-to-tencent-cloud.md` §8 上线清单未勾选

## 五、建议的修复顺序

1. **#1 + #2**：挡开发联调（SignalR 放行、启动降级）
2. **#4**：EF Core 10 升级（配合 #8 列名统一一起做，避免二次迁移）
3. **#3**：Redis 会话落地（上线前置）
4. **#5**：登录闭环单测
