namespace WildCourt.Api.Models;

/// <summary>
/// 庭院解锁配置行（Sheet16 YardUnlock / Config_YardUnlock.json，文档 03 §3.5.1）。
/// 与文档列名对齐：玩家等级 → 农田数/加工槽位/食物盆容量/装饰上限/展示柜格数/信箱容量。
/// </summary>
public class YardUnlockConfig
{
    public int Level { get; set; }

    public int FarmlandCount { get; set; }

    public int ProcessorSlots { get; set; }

    public int FoodBowlCapacity { get; set; }

    public int DecorationLimit { get; set; }

    public int ShowcaseSlots { get; set; }

    public int MailboxCapacity { get; set; }

    public string UnlockCond { get; set; } = string.Empty;
}

/// <summary>
/// 装饰物配置行（Config_Decorations.json，文档 03 §3.7）：D001 枫树 ...。
/// </summary>
public class DecorationConfig
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    /// <summary>类别：景观/家具/容器/建筑小品/展示架/宠物用品/地面装饰/节日限定。</summary>
    public string Category { get; set; } = string.Empty;

    /// <summary>售价（庭贝）。</summary>
    public int Price { get; set; }

    /// <summary>占格宽（1×1 / 2×2 / 1×3）。</summary>
    public int SizeX { get; set; } = 1;

    /// <summary>占格高。</summary>
    public int SizeY { get; set; } = 1;

    /// <summary>是否支持旋转（0°/90°/180°/270°）。</summary>
    public bool Rotatable { get; set; }

    /// <summary>获取方式：商店 / 节日活动等。</summary>
    public string Acquire { get; set; } = string.Empty;

    public string Description { get; set; } = string.Empty;

    /// <summary>可拥有/可摆放数量上限（当前统一 2，可配置不同值）。</summary>
    public int MaxCount { get; set; } = 2;

    /// <summary>粒子效果类型（空 = 无；如 leaves），客户端占位表现。</summary>
    public string Effect { get; set; } = string.Empty;
}
