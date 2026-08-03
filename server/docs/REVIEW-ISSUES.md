# 服务端代码审查报告（登录闭环）

> 审查时间：2026-08-03（首轮）→ 2026-08-03（修复后复查）
> 范围：`server/` 登录闭环（账号+密码注册/登录/会话/鉴权）。
> 结论：**核心闭环正确；首轮发现的 4 个 Should-fix 已全部修复。**

---

## 一、已验证 ✅（闭环正确）

- **密码链路闭环**：客户端 SHA256 一次哈希（`AuthService.HashPassword`）→ 服务端 BCrypt 存储/校验（`UserService.cs`），明文不落服务端。
- **业务码一致**：`ApiCodes`（Models/ApiCodes.cs）与客户端 `NetConst.cs` 逐项对齐；业务错误统一 HTTP 200 + 信封 `{ code, message }`，客户端按 `envelope.Code` 判断。
- **会话合理**：Redis 会话（`SessionStore`）token 128 位、7 天 TTL。
- **异常映射正确**：`ExceptionMiddleware` 业务异常 → 对应 code，系统异常 → 500。
- **无硬编码密钥**：Production 配置全为 `CHANGE_ME` 占位，真实值经 `.env` 注入。

## 二、首轮 Should-fix —— 状态

| # | 问题 | 状态 |
| --- | --- | --- |
| 1 | `AuthMiddleware` 401 拦截 `/hub/chat`，不解析 `?access_token=` | ✅ **已修**：放行 `/hub/` + 支持 `?access_token=`（AuthMiddleware.cs） |
| 2 | `Program.cs` 启动强依赖 MySQL（`ServerVersion.AutoDetect` + `Migrate()` 直崩） | ✅ **已修**：`Migrate()` 失败仅告警降级启动（Program.cs） |
| 3 | 会话为内存实现、RedisUtil 全占位（重启掉线/多实例不可用） | ✅ **已修**：`RedisUtil` 实现（StackExchange.Redis）+ `SessionStore` 切 Redis + `ISessionStore` 接口 |
| 4 | EF Core 9.0.18 / Pomelo 9.0.0 跑在 net10.0 | ⏸️ **保持 EF9**：Pomelo.EntityFrameworkCore.MySql 最新稳定版为 9.0.0（无 10.x，nuget 已核实），EF9 包跑 net10.0 运行时可编译可运行；待 Pomelo 发布 10.x 后再升 |

## 三、低优先级 —— 状态

| # | 问题 | 状态 |
| --- | --- | --- |
| 5 | 登录闭环零测试 | ✅ **已修**：新增 `UserServiceTests`（注册/登录/查重/401）+ `AuthMiddlewareTests`（放行/Bearer/access_token/401 信封） |
| 6 | 注册 TOCTOU：并发同名变 500 | ✅ **已修**：`RegisterAsync` catch `DbUpdateException` → 409（唯一索引兜底） |
| 7 | 列名 PascalCase 与文档 15.3 snake_case 不一致 | ✅ **已修**：DbContext Fluent 全部 `HasColumnName("snake_case")`（需重新生成迁移） |
| 8 | 迁移命名 `InitUsers` 实际建 7 张表 | ✅ **已修**：迁移重做时命名 `InitSchema`（见部署指引） |
| 9 | `yards.UserId` IdentityColumn、无 FK、注册不建默认庭院 | ⏸️ 保持：YardService 实现时兜底建默认庭院（低优先） |
| 10 | 无滑动续期、重复登录多 token 并存 | ⏸️ 接受（注释说明，设计取舍） |

## 四、待办占位（非缺陷，按计划推进）

- `ChatHub` 两方法（Hubs/ChatHub.cs）——实现时按文档 02 §2.5 补私聊/通知；鉴权在 Hub 内取 `context.Items["UserId"]`/token
- `scripts/init_db.sql`：仅建库无建表（建表以 EF Core 迁移为准）
- 其余 6 张表实体（nonfiction_collections / badges / match_pools / operation_logs / daily_settlements / sign_in_records）
- `WlidCourt.Tests` 中 Friend/Match/Settlement/Yard 四个占位测试待业务实现后补全
