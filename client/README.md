# 野庭漫邮 · 客户端框架

Q 版可爱风 · 全 2D 手绘 · 移动端异步社交手游。Unity 2022.3.62f3c1 + XLua + AssetBundle 热更。

> 设计依据：`docs/design/`（01~19）与 `docs/architecture/`（01~06）；目录结构见 `docs/design/15-项目目录结构.md`、`docs/architecture/06-目标目录结构.md`。

## 框架分层（Assets/Scripts）

| 层 | 目录 | 内容 |
| --- | --- | --- |
| 核心 | `Scripts/Core/` | `GameManager`（入口/初始化编排）、`EventBus`+`Events`（全局事件）、`ConfigMgr`（配置表只读加载）、`PlayerLevel`（玩家经验/等级）、`SignInManager`（签到本地状态） |
| 工具 | `Scripts/Utils/` | `TimeUtil`（UTC+8 / 04:00 结算）、`JsonUtil`（`{"rows":[...]}` 封装）、`MD5Util`（热更校验） |
| 网络 | `Scripts/Net/` | `Protocol/`（信封 + 按系统 DTO，对齐服务端 02 §2.2）、`NetManager`（HTTP，token/幂等/重试）、`Handler/`（按 `NetEvent` 路由到模块）、`WebSocket/`（SignalR 实时连接） |
| Lua 桥 | `Scripts/Lua/` | `LuaManager`（Init/HotReload/DoString/CallFunc/LoadConfig）、`LuaEnvHolder`、`LuaBehaviour`（面板生命周期桥） |
| UI | `Scripts/UI/Base/` | `BasePanel`（生命周期）、`UIManager`（面板开关/缓存） |
| 玩法 | `Scripts/Gameplay/` | 12 系统管理器（Crop/Processing/Cooking/Hospitality/Pet/Match/Nonfiction/Friend/Shop/Yard/Badge/DailySettlement）+ `GameplayManager<T>` 基类 |

## 启动流程

```
GameManager.Awake（单例常驻）
  └─ Boot():
     1. 本地版本检查（StreamingAssets/version.txt，缺失走全量热更）→ TODO(文档 14)
     2. ConfigMgr.LoadAll()        ← StreamingAssets/config/Config_*.json
     3. NetManager.Init(url, ws)   ← HTTP + SignalR（DefaultHandlers 注册推送路由）
     4. LuaManager.Init()          ← XLua 环境（USE_XLUA 宏）
     5. EventBus.Publish(GameReadyEvent) → 场景流 Launch → ProvinceSelect → Main（文档 03 §3.3）
```

联调：`GameManager` Inspector 中把 `serverUrl` 设为 `http://localhost:5000`（文档 17 §17.4）。

## XLua 启用方式

框架已用 `#if USE_XLUA` 隔离 XLua 依赖，**未定义宏时以 C# 桩模式运行，可正常编译**。启用热更 Lua：

1. 将 XLua 插件（`XLua.dll` + 生成代码）放入 `Assets/Plugins/XLua/`；
2. Player Settings → Other Settings → **Scripting Define Symbols** 添加 `USE_XLUA`；
3. 此时 `LuaManager`/`LuaBehaviour` 自动启用 XLua 宿主（开发期 `CustomLoader` 直读 `Assets/LuaScripts/`）。

## 实时连接（SignalR）接入

`Scripts/Net/WebSocket/SignalRClient.cs` 为可编译骨架。接入二选一：

1. **Microsoft.AspNetCore.SignalR.Client**（.NET Standard 2.0，推荐）——按 `OpenConnection()` 内注释注册 ChatHub 回调；
2. **WebSocketSharp**——手写 JSON 事件帧协议。

事件帧与 `NetEvent` 枚举（LetterArrived/FriendApply/Notice/DailySettlement/BadgeUnlocked/CurrencyChanged/ActivityChanged）一一对应，经 `HandlerCenter` 路由到模块并广播 `EventBus`。

## Lua 脚本（Assets/LuaScripts）

| 目录 | 内容 |
| --- | --- |
| `Logic/Match/MatchMaker.lua` | 匹配算法（文档 07 §7.7，数据源注入，服务端复用） |
| `Logic/MiniGame/Config_MiniGame.lua` | 小游戏配置（文档 10 §10.7） |
| `Logic/SignIn/Config_DailySignIn.lua` | 签到配置（文档 11 §11.6） |
| `Logic/Activity/Activity_MidAutumn.lua` | 活动配置示例（文档 13 §13.3） |
| `Logic/Farm/FarmManager.lua` | 种田逻辑：识别作物/时长、上报结束时间、倒计时成熟提醒（文档 04） |
| `Localization/LetterTemplates.lua` | 信件模板 T01~T05（文档 07 §7.10） |

## 配置表（StreamingAssets/config）

17 张表统一 `{ "rows": [...] }` 结构，由 `Excel2JSON.py` 导出（文档 16）。已落地：`Config_Crops.json`（Sheet3，14 种作物）、`Config_Shop.json`（Sheet9~12 种子部分）；已建骨架：`Config_PlayerLevel.json`（Sheet17）、`Config_YardUnlock.json`（Sheet16），数值为文档上下界插值，**待策划表确认后覆盖**（详见 `config/README.md`）。

## 美术资源形态

资源形态约定（文档 14/19）：

| 资源 | 形态 | 位置/加载 |
| --- | --- | --- |
| UI 相关图、地图/地面 | 美术预置静态图/预制体，**提前摆放**到场景或预制体（编辑器可见）；当前无美术时由代码生成占位（`YardPanel.BuildUI`、`YardRenderer.BuildGround`），替换点即这两处 | 场景/预制体 |
| 商品图（装饰商品图标 + 庭院摆放装饰） | 静态图，有图用图、无图分类色块；**异步加载** | `StreamingAssets/ui/Decorations/{id}.png`，`GoodsSprites.Request(id, cb)`（UnityWebRequestTexture，可热更） |
| 装饰帧动画 | 暂不做（商品图动态方案已取消） | - |

> 商品图目录后续纳入热更 manifest 增量覆盖（文档 14）。`Resources/Decorations` 为早期占位目录，已不再引用，后续随美术清理。

## 下一步 TODO
- [ ] 热更链路（文档 14）：`version.txt`/`manifest.json` MD5 对比 → 增量下载 → `LuaManager.HotReload`
- [ ] 接入 SignalR 客户端库并注册 ChatHub 回调
- [ ] 登录/注册流程（`UserController` → `NetManager.SetToken`）
- [ ] 场景搭建：Launch / ProvinceSelect / Main / Yard（文档 03 §3.3）
- [ ] UI 面板按系统填充（`Scripts/UI/{系统}/`），Lua UI 经 `LuaBehaviour` 绑定
- [ ] 各 Gameplay 管理器中 TODO 标注的玩法逻辑落地
- [ ] Excel2JSON.py 导出全部 17 张表
