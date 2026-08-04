namespace WlidCourt.Api.Models;

/// <summary>已摆放装饰项（文档 03 §3.8：等轴测网格 + 旋转）。</summary>
public class YardItemDto
{
    /// <summary>装饰 ID（D001 等，对应 Config_Decorations.json）。</summary>
    public string Id { get; set; } = string.Empty;

    /// <summary>网格 X（斜 45° 等轴测，X 轴向右下）。</summary>
    public int X { get; set; }

    /// <summary>网格 Y（Y 轴向右上）。</summary>
    public int Y { get; set; }

    /// <summary>旋转：0/90/180/270。</summary>
    public int Rotation { get; set; }
}

/// <summary>保存布局请求（PUT /api/v1/yard/layout，文档 03 §3.9 方案槽）。</summary>
public class SaveLayoutRequest
{
    /// <summary>布局槽 1~3。</summary>
    public int SlotIndex { get; set; }

    /// <summary>当前槽的已摆放装饰列表。</summary>
    public List<YardItemDto> Items { get; set; } = new();
}

/// <summary>一个方案槽的布局（服务端 Slots 为 List，便于客户端 JsonUtility 解析）。</summary>
public class YardSlotDto
{
    /// <summary>方案槽 1~3。</summary>
    public int SlotIndex { get; set; }

    public List<YardItemDto> Items { get; set; } = new();
}

/// <summary>庭院响应（GET /api/v1/yard、/visit/{userId}、PUT /layout）。</summary>
public class YardResponse
{
    public long UserId { get; set; }

    /// <summary>庭院等级（= 玩家等级，决定容量，文档 03 §3.5.1）。</summary>
    public int Level { get; set; }

    /// <summary>当前激活的方案槽（1~3）。</summary>
    public int CurrentSlot { get; set; }

    /// <summary>三个方案槽的布局。</summary>
    public List<YardSlotDto> Slots { get; set; } = new();

    /// <summary>当前等级容量（客户端显示上限用）。</summary>
    public YardUnlockConfig Cap { get; set; } = new();

    public DateTime UpdatedAt { get; set; }
}
