# Resources/Yard —— 庭院预制体搭建清单（美术预置方向）

> 纯客户端改造，不动服务端。`YardPanel.CreateOrInstantiate` 会先 `Resources.Load<GameObject>("Yard/YardPanel")`，
> 有预制体就走预制体（提前摆放），没有则回退运行时代码生成（MainScene 原样可跑）。

## 1. YardPanel 预制体

路径：`Assets/Resources/Yard/YardPanel.prefab`（根对象挂 `YardPanel` 组件）。

结构（层级从上到下）：
- 根（Canvas 或含 Canvas）：挂 `YardPanel`
  - Header：`Shell`（庭贝文字）、`Hint`（提示文字）、`Collapse`（收起按钮）
  - Tabs：9 个 Tab（顺序 = 全部/景观/家具/容器/建筑小品/展示架/宠物用品/地面装饰/节日限定）
  - Grid：商品格子容器
  - Actions：`Rotate` / `Mirror` / `Delete`（默认隐藏，选中装饰后显示）
  - Footer：`Prev` / `Page` / `Next` / `Slot1` / `Slot2` / `Slot3` / `Save`
  - Expand（左下角，默认隐藏）
  - ZoomOut / ZoomIn（右上角）
  - Grid 下放一个格子模板 `cellPrefab`（挂 `YardCell`）

### YardPanel 序列化字段绑定

| 字段 | 绑定 |
| --- | --- |
| `_shellText` / `_hintText` / `_pageText` | Header 的 Shell / Hint / Footer 的 Page |
| `_gridRoot` | Grid 容器 |
| `_cellPrefab` | Grid 下的格子模板 |
| `_collapseButton` / `_panelRoot` / `_expandButton` | Collapse 按钮 / 面板根 / Expand |
| `_prevButton` / `_nextButton` / `_saveButton` | Footer 的 Prev / Next / Save |
| `_slotButtons`（3 个） | Slot1~3 |
| `_rotateButton` / `_mirrorButton` / `_deleteButton` | Actions 的 Rotate / Mirror / Delete |
| `_zoomOutButton` / `_zoomInButton` | ZoomOut / ZoomIn |
| `_tabButtonsArray`（9 个）/ `_tabImagesArray`（9 个） | Tabs 的按钮与底色 Image |

### YardCell 格子模板（cellPrefab）

根挂 `YardCell`，字段：`bgImage`（底色）、`iconImage`（商品图）、`nameText`、`countText`（已建造 X/拥有 Y）、`remainText`（剩余 N/已满）、`buyButton`、`dragCell`（长按拖放，挂 `YardItemCell`）。

## 2. 地面（提前摆放）

- 场景里放一个地面 GameObject，挂 `YardGround`（要求 SpriteRenderer），Scale 约 (21, 10.5)，SortingOrder = -1000。
- 美术图放 `Assets/StreamingAssets/ui/Yard/ground.png`（可热更）；无图时 `YardGround` 自动用菱形占位。
- `YardRenderer` 检测到场景有 `YardGround` 就不再生成地面/围墙。

## 3. 入口

功能流程仍是 Splash → Login → MainScene；`YardSceneBootstrap` 会调用 `YardPanel.CreateOrInstantiate`。