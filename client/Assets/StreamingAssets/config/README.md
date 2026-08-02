# 配置表（Config）

本目录存放由 `Excel2JSON.py` 从 **17 张 Excel 表**导出的 JSON 配置，命名 `Config_*.json`。

- **客户端**：启动时由 `ConfigMgr` 只读加载（`Scripts/Core/ConfigMgr.cs`）
- **热更**：由 `version.txt` / `manifest.json` MD5 对比增量拉取（见 docs/design/14-热更新与每日结算系统.md）
- **服务端**：同步部署同一份 JSON（.NET 读取，数值与客户端一致）
- 导出链路与字段定义：见 docs/design/16-数值配置表规范.md

## 17 张表

Sheet1 Provinces · Sheet2 Cities · Sheet3 Crops · Sheet4 Fish · Sheet4.1 Bait
Sheet5 ProcessRecipes · Sheet6 FoodRecipes · Sheet7 Nonfictions · Sheet8 HospitalityWeights
Sheet9~12 Shop · Sheet13 FriendInteractions · Sheet14 StealMechanics · Sheet15 Badges
Sheet16 YardUnlock · Sheet17 PlayerLevel

> 示例：`Config_PlayerLevel.json`（玩家等级曲线，Sheet17）
