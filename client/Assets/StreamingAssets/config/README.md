# 配置表（Config）

本目录存放由 `Excel2JSON.py` 从 **17 张 Excel 表**导出的 JSON 配置，命名 `Config_*.json`。

- **客户端**：启动时由 `ConfigMgr` 只读加载（`Scripts/Core/ConfigMgr.cs`）
- **热更**：由 `version.txt` / `manifest.json` MD5 对比增量拉取（见 docs/design/14-热更新与每日结算系统.md）
- **服务端**：同步部署同一份 JSON（.NET 读取，数值与客户端一致）
- 导出链路与字段定义：见 docs/design/16-数值配置表规范.md

## JSON 结构约定

所有表统一为 `{ "rows": [ { ... }, ... ] }`（与 `ConfigMgr` / `JsonUtil.FromJsonArray` 对齐）。

## 17 张表清单

| 表 | 文件名 | 状态 |
| --- | --- | --- |
| Sheet1 Provinces | `Config_Provinces.json` | ⬜ 待导出 |
| Sheet2 Cities | `Config_Cities.json` | ⬜ 待导出 |
| Sheet3 Crops | `Config_Crops.json` | ✅ 已按文档 04 落地 14 种作物 |
| Sheet4 Fish | `Config_Fish.json` | ⬜ 待导出 |
| Sheet4.1 Bait | `Config_Bait.json` | ⬜ 待导出 |
| Sheet5 ProcessRecipes | `Config_ProcessRecipes.json` | ⬜ 待导出 |
| Sheet6 FoodRecipes | `Config_FoodRecipes.json` | ⬜ 待导出 |
| Sheet7 Nonfictions | `Config_Nonfictions.json` | ⬜ 待导出 |
| Sheet8 HospitalityWeights | `Config_HospitalityWeights.json` | ⬜ 待导出 |
| Sheet9~12 Shop | `Config_Shop.json` | ✅ 种子货架已落地（装饰仍走 Config_Decorations.json） |
| Sheet13 FriendInteractions | `Config_FriendInteractions.json` | ⬜ 待导出 |
| Sheet14 StealMechanics | `Config_StealMechanics.json` | ⬜ 待导出 |
| Sheet15 Badges | `Config_Badges.json` | ⬜ 待导出 |
| Sheet16 YardUnlock | `Config_YardUnlock.json` | ✅ 已建骨架（数值待策划确认） |
| Sheet17 PlayerLevel | `Config_PlayerLevel.json` | ✅ 已建骨架（经验曲线待策划确认） |

> 已建骨架的两张表（Sheet16/17）为玩家等级与庭院容量的前置依赖（文档 06 §6.4），
> 数值为文档约定的上下界插值，正式值由 Excel2JSON.py 导出后覆盖。

> `Config_Crops.json`（Sheet3）与 `Config_Shop.json`（Sheet9~12 种子部分）已按文档 04/09 落地真实数值，
> 其余货架（材料/道具/鱼饵）待对应 Excel 表导出后补充。
