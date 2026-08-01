# 野庭漫邮（WlidCourt）· 代码架构设计文档

> 依据 `docs/design/` 全部 19 份策划案 + README 提炼生成的代码架构蓝图。
> 生成时间：V0.1.0-alpha 基线。实现过程中如策划案有更新，以最新策划案为准。

## 文档清单

| 文件 | 内容 | 主要依据 |
| --- | --- | --- |
| [01-总体架构.md](01-总体架构.md) | 技术栈、系统分层、C4 上下文图、核心玩法闭环、设计原则 | 文档 01/02/15/16/17 |
| [02-服务端架构.md](02-服务端架构.md) | .NET 分层：Controller / Service / Job / Hub / Middleware / Utils，类职责与核心方法设计 | 文档 07/10/11/13/14/15 |
| [03-客户端架构.md](03-客户端架构.md) | Unity 脚本分层、XLua + AssetBundle 热更流程、Lua 配置组织、网络层 | 文档 03~14、15、16 |
| [04-数据与调度架构.md](04-数据与调度架构.md) | MySQL 13 表、Redis 8 Key、4 个定时 Job、每日结算十项 | 文档 14/15/16 |
| [05-模块依赖与数据流.md](05-模块依赖与数据流.md) | 主循环链路、跨系统依赖关系、关键时序、经济闭环 | 文档 01/03/04/05/07/08/09/13 |
| [06-目标目录结构.md](06-目标目录结构.md) | 客户端 + 服务端 + 配置表完整目标目录树 | 文档 15/16 |

## 与现状的关系

- `client/` 已按文档十五搭好 Unity 目录骨架（`Assets/Scripts` 下 Core/UI/Gameplay/Net/Utils/Lua，`LuaScripts` 下 Config/Logic/UI/Localization），本文档给出的是**目标态**的类级落位。
- `server/` 仅有 `WlidCourt.Api` / `WlidCourt.Data` / `WlidCourt.Tests` 三项目的一层目录骨架，本文档给出各类的职责与核心方法签名，可直接指导编码。

## 核心速览

```
种植 → 加工 → (口粮 + 非遗) → 食物盆 → 好客值 → 匹配池 → 派遣 → 访问
   → 留信/留非遗 → 回赠 → 加好友 → 互访(偷菜/喂食) → 图鉴/徽章
```

- 客户端：Unity 2022.3.62f3c1，全 2D 手绘，XLua + AssetBundle 热更
- 服务端：.NET 10 Web API（WlidCourt.Api / WlidCourt.Data / WlidCourt.Tests）
- 存储：MySQL 13 表 + Redis 8 Key（会话/匹配池/队列）
- 调度：Quartz 4 个 Job（每日结算 / 匹配池刷新 / 活动结束 / 信件审核）
