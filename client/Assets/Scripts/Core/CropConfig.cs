using System;

/// <summary>
/// 作物配置（只读）。
/// 字段对应文档十六 Sheet3 Crops：
/// id / name / growTime(分钟) / yield / sellPrice / processTo / processTime(分钟)。
/// </summary>
[Serializable]
public class CropConfig
{
    public string id;          // 作物ID，如 C01
    public string name;        // 名称，如 小麦
    public int growTime;       // 生长时间（分钟）
    public int yield;          // 产量
    public int sellPrice;      // 售价（庭贝/个）
    public string processTo;   // 加工产物ID（无则为空）
    public int processTime;    // 加工时间（分钟，无则 0）
}
