using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 数值配置管理。
/// 文档十六：配置由 Excel 经 Python 导出为 Lua 后加载。
/// 当前阶段先在 C# 侧持有内存结构以跑通逻辑，后续接入 XLua 后改为读取 Lua。
/// </summary>
public class ConfigManager
{
    public Dictionary<string, CropConfig> Crops { get; } = new Dictionary<string, CropConfig>();

    public void LoadAll()
    {
        LoadCrops();
        // TODO: 接入 Lua 加载（XLua）后，由导出的 Config_Crop.lua 填充。
        Debug.Log($"[ConfigManager] 作物配置加载完成，共 {Crops.Count} 条。");
    }

    private void LoadCrops()
    {
        // 占位：实际数据来自导出的 Lua/Excel。这里按文档四 4.2 的 14 种作物结构预置骨架。
        // 字段对应文档十六 Sheet3 Crops：id/name/growTime/yield/sellPrice/processTo/processTime
        // TODO: 删除占位，改为运行时读取导出配置。
    }

    public CropConfig GetCrop(string cropId)
    {
        return Crops.TryGetValue(cropId, out var c) ? c : null;
    }
}
