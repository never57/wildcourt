using System;

/// <summary>
/// 装饰物配置（只读，Config_Decorations.json）。
/// 字段对应文档 03 §3.7：id / name / category / price / sizeX×sizeY(格) / rotatable / acquire / description。
/// </summary>
[Serializable]
public class DecorationConfig
{
    public string id;            // 装饰ID，如 D001
    public string name;          // 名称，如 枫树
    public string category;      // 类别：景观/家具/容器/建筑小品/展示架/宠物用品/地面装饰/节日限定
    public int price;            // 售价（庭贝）
    public int sizeX;            // 占格宽（1×1 / 2×2 / 1×3）
    public int sizeY;            // 占格高
    public bool rotatable;       // 是否支持旋转（0°/90°/180°/270°）
    public string acquire;       // 获取方式：商店/节日活动
    public string description;   // 描述
    public int maxCount = 2;     // 可拥有/可摆放数量上限（当前统一 2）
    public string effect = "";   // 粒子效果类型（空=无，如 leaves）
}
