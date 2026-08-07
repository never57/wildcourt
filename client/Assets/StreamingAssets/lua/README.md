# StreamingAssets/lua —— 热更 Lua

本目录是**运行时热更 Lua** 的目标目录（文档 14 §14.1）：

| 内容 | 目录 |
| --- | --- |
| Lua 逻辑 / 玩法脚本 | `lua/`（`lua/Logic/Match/MatchMaker.lua` 等） |
| UI 脚本 | `lua/UI/` |
| 活动配置 | `lua/Logic/Activity/` |

## 与 Assets/LuaScripts 的关系

- 开发期：Lua 源码维护在 `Assets/LuaScripts/`（`LuaEnvHolder.CustomLoader` 开发路径直读）；
- 发布期：构建/热更流水线把 `Assets/LuaScripts/` **同步/打包**到 `StreamingAssets/lua/`（结构与 `LuaScripts/` 一致）；
- 热更：manifest.json 中带 MD5，增量拉取覆盖 `persistentDataPath`，`LuaManager.HotReload()` 重载（失败回滚上一版本，文档 14 §14.4）。

## 已部署模块（首个可热更版本）

| 模块（require 路径） | 文件 |
| --- | --- |
| `Logic.Match.MatchMaker` | `lua/Logic/Match/MatchMaker.lua` |
| `Logic.MiniGame.Config_MiniGame` | `lua/Logic/MiniGame/Config_MiniGame.lua` |
| `Logic.SignIn.Config_DailySignIn` | `lua/Logic/SignIn/Config_DailySignIn.lua` |
| `Logic.Activity.Activity_MidAutumn` | `lua/Logic/Activity/Activity_MidAutumn.lua` |
| `Localization.LetterTemplates` | `lua/Localization/LetterTemplates.lua` |

新增热更脚本时：放入 `Assets/LuaScripts/` 对应目录并同步到本目录，同时在 `LuaManager.HotModules` 清单中登记 require 路径。
