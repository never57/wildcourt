# 野庭漫邮 Lua API 接口参考

> 供后续各玩法系统（种植 / 加工 / 烹饪 / 招待 / 宠物 / 匹配 / 科普 / 好友 / 商铺 / 庭院 / 徽章 / 每日结算）在 Lua 层调用。
> 本文档描述 **Lua ↔ C# 桥接层** 暴露给脚本的 API 契约（非服务端 REST 文档，服务端接口见 `server/docs/api.yaml`）。
>
> 约定：
> - 所有 API 挂在全局表 `WC` 下，按能力分模块：`WC.net` / `WC.config` / `WC.event` / `WC.ui` / `WC.push`。
> - 所有网络请求均自动注入登录 token（`NetManager` 持有），无需手动传 token。
> - 回调统一为 `function(resp)`，其中 `resp = { code=number, data=any, msg=string }`。
> - 错误码（`WC.net.CODE`）与服务端 `ApiCodes` 对齐，见下文 §1.4。
> - 宏开关：桥接实现用 `#if USE_XLUA` 隔离，未启用 XLua 时以 C# 桩运行，但本文档的调用约定不变。

---

## 1. `WC.net` — 网络请求

### 1.1 基础方法 

| 方法 | 签名 | 说明 |
|---|---|---|
| `get` | `WC.net.get(path, query?, cb)` | GET 请求，`query` 为可选 table（拼到 URL query） |
| `post` | `WC.net.post(path, body?, cb)` | POST 请求，`body` 为 table（JSON 序列化） |
| `put` | `WC.net.put(path, body?, cb)` | PUT 请求，同 post |

```lua
-- 示例：拉取庭院
WC.net.get(WC.net.API.YardGet, nil, function(resp)
    if resp.code == WC.net.CODE.Success then
        -- resp.data 为服务端返回的庭院数据
        print("庭院布局:", resp.data.layout)
    else
        print("失败:", resp.msg)
    end
end)
```

### 1.2 端点常量 `WC.net.API`

常量值与 C# `Net.Protocol.ApiPaths` 一一对应（见 `client/Assets/Scripts/Net/Protocol/NetConst.cs`）。下表为当前已约定的路径（`Base = /api/v1`）：

**用户 UserController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `UserRegister` | `/api/v1/user/register` | POST |
| `UserLogin` | `/api/v1/user/login` | POST |
| `UserProfile` | `/api/v1/user/profile` | GET |
| `UserLevel` | `/api/v1/user/level` | GET |
| `UserExp` | `/api/v1/user/exp` | GET |
| `UserInventory` | `/api/v1/user/inventory` | GET |

**宠物 PetController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `PetInfo` | `/api/v1/pet/info` | GET |
| `PetDispatch` | `/api/v1/pet/dispatch` | POST |
| `PetRecall` | `/api/v1/pet/recall` | POST |

**信件 LetterController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `LetterSend` | `/api/v1/letter/send` | POST |
| `LetterInbox` | `/api/v1/letter/inbox` | GET |
| `LetterGiftBack` | `/api/v1/letter/gift-back` | POST |
| `LetterStatus(id)` | `/api/v1/letter/status/{id}` | GET |

**好友 FriendController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `FriendApply` | `/api/v1/friend/apply` | POST |
| `FriendAccept` | `/api/v1/friend/accept` | POST |
| `FriendSteal` | `/api/v1/friend/steal` | POST |
| `FriendFeed` | `/api/v1/friend/feed` | POST |
| `FriendYard(id)` | `/api/v1/friend/yard/{id}` | GET |

**商铺 ShopController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `ShopShelves` | `/api/v1/shop/shelves` | GET |
| `ShopBuy` | `/api/v1/shop/buy` | POST |
| `ShopBaitShelf` | `/api/v1/shop/bait-shelf` | GET |

**庭院 YardController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `YardGet` | `/api/v1/yard` | GET |
| `YardLayout` | `/api/v1/yard/layout` | PUT |
| `YardPlant` | `/api/v1/yard/plant` | POST |
| `YardPlots` | `/api/v1/yard/plots` | GET |
| `YardVisit(id)` | `/api/v1/yard/visit/{id}` | GET |

**活动 ActivityController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `ActivityList` | `/api/v1/activity/list` | GET |
| `ActivityClaim(id)` | `/api/v1/activity/claim/{id}` | POST |

**签到 DailySignInController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `SignInToday` | `/api/v1/signin/today` | GET |
| `SignInClaimWeek` | `/api/v1/signin/claim-week` | POST |
| `SignInClaimMonth` | `/api/v1/signin/claim-month` | POST |

**小游戏 MiniGameController**
| 常量 | 路径 | 方法 |
|---|---|---|
| `MiniGameStart(id)` | `/api/v1/minigame/start/{id}` | POST |
| `MiniGameFinish` | `/api/v1/minigame/finish` | POST |
| `MiniGameDailyCap` | `/api/v1/minigame/daily-cap` | GET |

> 每个方法对应 `NetManager` 的 `Get/Post/Put`，并自动注入 token；路径常量由 `ApiPaths` 提供字符串，Lua 侧直接读取。

### 1.3 回调 resp 结构

```lua
resp = {
    code = 0,        -- 见 §1.4
    data = {},       -- 成功时的业务数据（table / number / string，按接口而定）
    msg  = "",       -- 失败时的错误描述
}
```

### 1.4 错误码 `WC.net.CODE`（对齐 `Net.Protocol.ApiCodes`）

| 字段 | 值 | 含义 |
|---|---|---|
| `Success` | `0` | 成功 |
| `BadRequest` | `400` | 参数错误 |
| `Unauthorized` | `401` | 未登录 / token 失效 |
| `Forbidden` | `403` | 无权限 |
| `NotFound` | `404` | 资源不存在 |
| `Conflict` | `409` | 状态冲突（如账号已存在） |
| `ServerError` | `500` | 服务端内部错误 |
| `NetworkError` | `-1` | 客户端本地：网络不通 |
| `Timeout` | `-2` | 客户端本地：请求超时 |

---

## 2. `WC.config` — 配置表查询

读取 `StreamingAssets/config/Config_*.json`（由 `ConfigMgr` 加载）。配置表统一为 `{ rows = [...] }` 结构。

| 方法 | 签名 | 说明 |
|---|---|---|
| `getTable` | `WC.config.getTable(name) -> table` | 返回整张表（`rows` 数组），`name` 如 `"Crop"`、`"Shop"` |
| `getRow` | `WC.config.getRow(name, pred) -> table|nil` | 按 `pred(row)` 返回首条匹配行（Lua 函数做断言） |
| `getRowById` | `WC.config.getRowById(name, id) -> table|nil` | 按 `id` 字段快速取行（常用快捷方式） |

```lua
-- 示例：查作物 C01 的配置
local crop = WC.config.getRowById("Crops", "C01")
if crop then
    print("作物名:", crop.name, "生长时间(分钟):", crop.growTime)
end
```

> `name` 与 `Config_*.json` 文件名（去 `Config_` 前缀、`json` 后缀）对应。新增数值表时同时在 `ConfigMgr` 注册加载并登记到本文档。
> 当前已登记表：`Crops`（Config_Crops.json）、`Shop`（Config_Shop.json）、`Decorations` / `PlayerLevel` / `YardUnlock`。

---

## 3. `WC.event` — 全局事件（EventBus）

发布 / 订阅 `EventBus` 全局事件（见 `client/Assets/Scripts/Core/EventBus.cs`）。事件名见 `Core/Events.cs` 中的常量。

| 方法 | 签名 | 说明 |
|---|---|---|
| `on` | `WC.event.on(name, cb) -> handle` | 订阅事件，返回句柄用于取消 |
| `off` | `WC.event.off(name, handle)` | 取消订阅 |
| `publish` | `WC.event.publish(name, ...)` | 发布事件，透传后续参数给订阅者 |

```lua
local h = WC.event.on("GameReady", function()
    print("进入游戏，开始拉取庭院")
    WC.net.get(WC.net.API.YardGet, nil, onYardLoaded)
end)
-- 不再需要时
WC.event.off("GameReady", h)
```

> 常见事件名（与 `Core/Events.cs` 对齐）：`GameReady`、`ConfigLoaded`、`LoggedIn`、`CurrencyChanged` 等。新增全局事件请登记到 `Events.cs` 并补在此处。

---

## 4. `WC.ui` — 面板管理

操作 `UI/Base` 下的 `BasePanel` 与 `UIManager`（面板缓存复用）。

| 方法 | 签名 | 说明 |
|---|---|---|
| `open` | `WC.ui.open(panelName, param?)` | 打开面板，`param` 可选传给 `OnEnter` |
| `close` | `WC.ui.close(panelName)` | 关闭面板 |
| `closeAll` | `WC.ui.closeAll()` | 关闭所有已开面板 |
| `isOpen` | `WC.ui.isOpen(panelName) -> bool` | 查询面板是否打开 |

```lua
WC.ui.open("ShopPanel", { shelfId = 1 })
-- 面板逻辑在 Lua 侧以 WC.ui 回调 + LuaBehaviour 生命周期绑定
```

> 面板名与 `UI/` 下各系统目录的预制体 / `BasePanel` 子类对应（如 `UI/Shop/ShopPanel`）。

---

## 5. `WC.push` — 实时推送（SignalR）

订阅服务端经 `ChatHub` 推送的实时事件（经 `Net/Handler/HandlerCenter` 路由到 `NetEvent`）。事件类型对齐 `Net.Protocol.NetEvent`。

| 方法 | 签名 | 说明 |
|---|---|---|
| `on` | `WC.push.on(evt, cb) -> handle` | 订阅推送事件，`evt` 为字符串，`cb(data)` 收到事件负载（table） |
| `off` | `WC.push.off(evt, handle)` | 取消订阅 |

**可订阅事件 `evt` 字符串**（与 `NetEvent` 枚举对齐）：

| evt | 含义 | 载荷说明 |
|---|---|---|
| `"LetterArrived"` | 新信件送达（含随附非遗） | 信件摘要 table |
| `"FriendApply"` | 收到好友申请 | 申请人信息 table |
| `"Notice"` | 通用通知（访问留下 / 空盆离开等） | 通知文本 / 类型 |
| `"DailySettlement"` | 每日 04:00 结算播报 | 结算汇总 table |
| `"BadgeUnlocked"` | 徽章解锁 | 徽章 id |
| `"CurrencyChanged"` | 货币变化（庭贝 / 邮券） | `{ currency, delta, total }` |
| `"ActivityChanged"` | 活动激活 / 关闭 | 活动 id + 状态 |

```lua
local h = WC.push.on("CurrencyChanged", function(data)
    print("货币变化:", data.currency, data.delta, "当前:", data.total)
end)
```

---

## 5.5 `WC.timer` — 计时器

毫秒级定时器（协程实现，主线程安全），供 Lua 侧做倒计时/延迟回调。

| 方法 | 签名 | 说明 |
|---|---|---|
| `setTimeout` | `WC.timer.setTimeout(cb, ms) -> handle` | `ms` 毫秒后执行 `cb`，返回句柄 |
| `clearTimeout` | `WC.timer.clearTimeout(handle)` | 取消定时器（已执行/已取消时静默） |

```lua
local handle = WC.timer.setTimeout(function()
    print("作物成熟！")
end, 5 * 60 * 1000)
-- 不再需要时
WC.timer.clearTimeout(handle)
```

---

## 6. 约定与扩展

1. **新增 REST 接口**：先在服务端 `Controllers` 加端点 → 同步在 `Net.Protocol.NetConst.cs` 的 `ApiPaths` 加常量 → 在本文档 §1.2 登记 `WC.net.API` 项。
2. **新增配置表**：`Config_*.json` 放入 `StreamingAssets/config/` → `ConfigMgr` 注册 → 在本文档 §2 登记表名。
3. **新增全局事件**：在 `Core/Events.cs` 加常量 → 本文档 §3 登记。
4. **新增推送事件**：在 `Net.Protocol.NetEvent` 加枚举 → `HandlerCenter` 注册处理器 → 本文档 §5 登记。
5. **命名风格**：常量全英文 PascalCase，方法 camelCase，文档中文描述。所有 Lua 入口统一挂 `WC` 全局表，避免污染全局命名空间。
6. **错误处理**：所有网络 / 推送回调需先判 `resp.code == WC.net.CODE.Success`，否则按 `resp.msg` 提示；`code` 为负表示客户端本地错误（网络 / 超时），应做重试或提示。

---

## 7. 服务端 REST API 用法

本节说明**服务端实际接口**的调用方式（供服务端联调 / 客户端网络层实现参考）。所有接口前缀 `Base = /api/v1`。

### 7.1 通用约定

- **Base URL**：`http://{host}:{port}/api/v1`（联调 `http://localhost:5000/api/v1`；线上 `http://43.138.133.137:5000/api/v1`）
- **请求体**：`application/json`，字段用 camelCase。
- **鉴权**：除 `register` / `login` 外，所有接口需在 Header 携带 `Authorization: Bearer {token}`（`token` 来自 `login` 返回，客户端 `NetManager` 自动注入）。
- **响应信封**（统一）：

```json
{ "code": 0, "data": { }, "message": "" }
```

  - `code == 0` 成功，`data` 为业务数据；非 0 见错误码表（§1.4，`code` 与服务端 `ApiCodes` 对齐）。
  - 客户端注意：服务端字段名为 `message`（非 `msg`），桥接层 `WC.net` 回调已统一转成 `resp.msg`，Lua 无需关心差异。

### 7.2 鉴权流程（先 register/login 拿 token）

**注册** `POST /api/v1/user/register`
```json
// 请求
{ "username": "alice", "password": "<SHA256(明文密码)>" }
// 响应 200
{ "code": 0, "data": { "id": 1001, "username": "alice", "level": 1, "exp": 0 }, "message": "" }
```

**登录** `POST /api/v1/user/login`（返回 token，后续请求带上）
```json
// 请求
{ "username": "alice", "password": "<SHA256(明文密码)>" }
// 响应 200
{ "code": 0, "data": { "token": "eyJ...", "userId": 1001 }, "message": "" }
```
> 密码约定：客户端 `AuthService` 先做 SHA256，服务端再用 BCrypt 存储，因此请求体里传的是哈希值。

### 7.3 用户 UserController `api/v1/user`

| 方法 & 路径 | 说明 | 请求 | 响应 data |
|---|---|---|---|
| `GET /profile` | 玩家信息（需 token） | — | `User`（id/username/level/exp/...） |
| `GET /level` | 等级+经验 | — | `{ level:int, exp:int }` |
| `POST /exp` | 联调加经验（alpha，后续替换） | `{ "exp": 100 }` | `{ level:int, exp:int }` |
| `GET /inventory` | 背包快照 | — | `InventoryResponse`（已拥有物品列表） |

```bash
# 示例：带 token 取玩家等级
curl -H "Authorization: Bearer <token>" \
     http://localhost:5000/api/v1/user/level
# => {"code":0,"data":{"level":3,"exp":420},"message":""}
```

### 7.4 庭院 YardController `api/v1/yard`

| 方法 & 路径 | 说明 | 请求 | 响应 data |
|---|---|---|---|
| `GET /` | 我的庭院（3 槽布局+容量） | — | `YardResponse` |
| `PUT /layout` | 保存某槽布局（服务端校验容量/重叠） | `SaveLayoutRequest`（见下） | `YardResponse` |
| `POST /plant` | 种植作物（客户端 Lua 计算并上报结束时间） | `PlantCropRequest`（见下） | `FarmPlotDto` |
| `GET /plots` | 我的农田种植状态（恢复本地倒计时） | — | `FarmPlotsResponse` |
| `GET /visit/{userId}` | 互访他人庭院 | — | `YardResponse` |

**`PlantCropRequest` 结构**
```json
{
  "cellId": 0,
  "cropId": "C01",
  "readyAt": 1789999999
}
```
> `cellId`：农田格（0 起，上限为玩家等级对应的 `farmlandCount`）；`readyAt`：作物结束时间（Unix 秒，客户端按 `now + growTime*60` 计算）。响应 `FarmPlotDto`：`{ cellId, cropId, readyAt, state }`，`state` 为 `growing` / `mature`。

**`SaveLayoutRequest` 结构**
```json
{
  "slotIndex": 1,
  "items": [
    { "id": "D001", "x": 3, "y": 2, "rotation": 90, "mirror": false }
  ]
}
```
> `YardItemDto`：`id`(装饰ID，对应 Config_Decorations.json)、`x/y`(等轴测网格坐标)、`rotation`(0/90/180/270)、`mirror`(水平翻转)。`YardResponse.Cap` 为当前等级容量（客户端显示上限用）。

```lua
-- 在 Lua 中调用（对应 WC.net.put）
WC.net.put(WC.net.API.YardLayout, {
    slotIndex = 1,
    items = { { id = "D001", x = 3, y = 2, rotation = 90, mirror = false } }
}, function(resp)
    if resp.code == WC.net.CODE.Success then
        print("布局已保存，当前槽:", resp.data.currentSlot)
    end
end)
```

### 7.5 商铺 ShopController `api/v1/shop`

| 方法 & 路径 | 说明 | 请求 | 响应 data |
|---|---|---|---|
| `GET /shelves` | 货架列表（装饰+庭贝） | — | `List<ShelfItem>` |
| `POST /buy` | 购买（双货币：庭贝/邮券） | `BuyRequest`（见下） | `BuyResponse` |
| `GET /bait-shelf` | 鱼饵货架 | — | （待实现） |

**`BuyRequest` 结构**
```json
{ "itemId": "D001", "count": 1, "currency": "coin" }
```
> `currency`：`coin`(庭贝) / `ticket`(邮券)。
> 阶段 B 起 `/buy` 同时支持装饰（Config_Decorations.json）与种子（Config_Shop.json，`category=seed`）；种子无数量上限，装饰仍按 `maxCount` 限制。

### 7.6 信件 LetterController `api/v1/letter`（骨架，待实现）

| 方法 & 路径 | 说明 |
|---|---|
| `POST /send` | 写信（50 字 + 随附非遗），请求体待定 |
| `GET /inbox` | 收件箱，`List<Letter>` |
| `POST /gift-back` | 回赠，请求体待定 |
| `GET /status/{letterId}` | 审核状态，`Letter` |

### 7.7 每日签到 DailySignInController `api/v1/signin`（骨架，待实现）

| 方法 & 路径 | 说明 |
|---|---|
| `GET /today` | 今日签到状态 |
| `POST /claim-week` | 领取周签到 |
| `POST /claim-month` | 领取月签到 |

### 7.8 宠物 / 好友 / 活动 / 小游戏（端点见 §1.2）

- `PetController` `api/v1/pet`：`GET /info`、`POST /dispatch`、`POST /recall`（骨架）
- `FriendController` `api/v1/friend`：`POST /apply`、`POST /accept`、`POST /steal`、`POST /feed`、`GET /yard/{id}`（骨架）
- `ActivityController` `api/v1/activity`：`GET /list`、`POST /claim/{id}`（骨架）
- `MiniGameController` `api/v1/minigame`：`POST /start/{id}`、`POST /finish`、`GET /daily-cap`（骨架）
- `AdminController` `api/v1/admin`：管理端（骨架）

> 标注「骨架 / 待实现」的接口在 Controller 中目前 `throw new NotImplementedException()`，联调时会返回 500；接入前需先在服务端落地对应 Service。

### 7.9 字段类型速查

| 类型 | 来源 | 说明 |
|---|---|---|
| `User` | `Data/Entities/User.cs` | id/username/level/exp/province/city/identity 等 |
| `YardResponse` / `YardItemDto` / `YardSlotDto` / `SaveLayoutRequest` | `Models/YardDtos.cs` | 庭院与布局 |
| `ShelfItem` / `BuyRequest` / `BuyResponse` | `Models/*` | 商店 |
| `RegisterRequest` / `LoginRequest` / `LoginResponse` / `AddExpRequest` | `Models/AuthDtos.cs` | 鉴权 |
| `Letter` | `Data/Entities/Letter.cs` | 信件 |

---

## 8. 客户端 C# ↔ Lua 互调 API

本节描述**客户端内部** C# 与 Lua 之间的桥接机制（XLua），即「Lua 怎么调 C#」「C# 怎么调 Lua」。上层 `WC.*`（§2–§6）正是 C# 暴露给 Lua 的统一结果，本节说明其底层原理与直接调用方式。

### 8.1 宏开关与桩行为

- 所有 XLua 相关代码用 `#if USE_XLUA` / `#endif` 隔离（见 `LuaManager` / `LuaBehaviour` / `LuaEnvHolder`）。
- 未定义 `USE_XLUA` 宏时进入**桩模式**：`LuaBehaviour` 不执行 Lua 函数，仅打日志（`[LuaBehaviour] 桩模式: {module}.{func}`），`LuaManager` 返回空结果——保证工程在没装 XLua 插件时也能编译、运行 UI 框架。
- 启用热更：在 Player Settings 定义 `USE_XLUA` 宏并导入 `Assets/XLua/` 插件。

### 8.2 Lua 调 C#（C# → Lua 暴露）

两种暴露方式：

**(A) 约定式全局表 `WC`（推荐，见 §2–§6）**

由 `LuaManager.Init` 在 LuaEnv 的 global 上注册 `WC = { net={}, config={}, event={}, ui={}, push={} }`，把 C# 方法包成 Lua 函数。Lua 侧直接 `WC.net.get(...)`、`WC.event.on(...)`。泛型方法（`EventBus.Subscribe<T>`、`UIManager.Open<T>`）在 Lua 里无法写 `<T>`，一律由 `WC.*` 包装后调用，Lua 不需要感知泛型。

**(B) 直接导出 C# 类 `[LuaCallCSharp]`（进阶）**

对需在 Lua 中直接 new / 调方法的 C# 类型，用特性标记：
```csharp
using XLua;
[LuaCallCSharp]
public class SomeHelper { public int Add(int a, int b) => a + b; }
```
Lua 侧即可 `local h = CS.WildCourt.SomeHelper(); print(h:Add(1,2))`。当前项目主要在 `WC` 表暴露，避免 Lua 直接依赖 C# 命名空间（热更隔离更好）。

### 8.3 C# 调 Lua（Lua 生命周期桥）

**(a) LuaBehaviour（UI 热更桥）** — `client/Assets/Scripts/Lua/LuaBehaviour.cs`

挂在 UI 预制体上，`luaModule` 填 Lua 模块路径（如 `UI.YardPanel` → `Assets/LuaScripts/UI/YardPanel.lua`）。GameObject 生命周期自动映射到 Lua 表同名函数：

| C# 时机 | 调用 Lua 函数 | 说明 |
|---|---|---|
| `Start` | `OnInit` | 模块绑定并初始化 |
| `OnEnable` | `OnOpen` | 面板显示 |
| `OnDisable` | `OnClose` | 面板隐藏 |
| `OnDestroy` | `OnDispose` | 卸载清理 |

```lua
-- Assets/LuaScripts/UI/YardPanel.lua
local YardPanel = {}
function YardPanel.OnInit()   print("YardPanel 初始化") end
function YardPanel.OnOpen()   WC.net.get(WC.net.API.YardGet, nil, onYard) end
function YardPanel.OnClose()  end
function YardPanel.OnDispose() end
return YardPanel
```
> 运行时也可 `luaBehaviour.Bind("UI.YardPanel")` 动态绑定；模块找不到时仅 warning，不中断。

**(b) LuaManager 主动执行** — `client/Assets/Scripts/Lua/LuaManager.cs`

| 方法 | 签名 | 用途 |
|---|---|---|
| `DoString` | `object[] DoString(string chunk, string chunkName="")` | 直接执行一段 Lua 代码 |
| `LoadFile` | `LuaTable LoadFile(string filePath)` | 加载并返回 Lua 模块 table |
| `DoFile` | `object[] DoFile(string filePath)` | 加载并执行 Lua 文件 |
| `Tick` | `void Tick()` | 每帧驱动 Lua（coroutine / timer） |

```csharp
// C# 侧启动 Lua 主入口
LuaManager.Instance.DoFile("Main");  // 对应 Assets/LuaScripts/Main.lua
```

### 8.4 三层 API 关系总览

```
服务端 REST  ──(HTTP/SignalR)──►  NetManager / HandlerCenter (C#)
                                         │ 解析后
                                         ▼
                              EventBus.Publish / 缓存快照 (C#)
                                         │ 暴露
                                         ▼
                          WC.* 全局表 (C# 包成 Lua 函数)
                                         │
                                         ▼
                               Lua 业务脚本 (各系统)
                                         │ 调 C# 生命周期
                                         ▼
                              LuaBehaviour.OnInit/OnOpen/... (C# 桥)
```
- §7 是「服务端 ↔ 客户端(C#)」契约；
- 本文 §2–§6 是「C# → Lua 暴露」契约（`WC.*`）；
- 本节 §8 是「Lua ↔ C# 互调」的底层机制与直接调用方式。

---

## 9. 待办（骨架阶段）

- [x] `WC.net` / `WC.config` / `WC.timer` 桥接已实现（`Scripts/Lua/WCBridge.cs` + `LuaTimerService.cs`，`LuaManager.Init` 自动挂载）
- [ ] `WC.event` / `WC.ui` / `WC.push` 桥接（待实现）
- [ ] 各系统 Lua 脚本（匹配 / 签到 / 活动 / 信件）接入上述 API
- [ ] 服务端 `api.yaml` 补全端点（与 §1.2 / §7 对齐），并启用 Swagger 以便联调
- [ ] 待实现 Controller（Letter/SignIn/Pet/Friend/Activity/MiniGame/Admin）落地 Service
- [ ] 明确哪些 C# 类型需要 `[LuaCallCSharp]` 导出（除 `WC` 表外的进阶用法）
