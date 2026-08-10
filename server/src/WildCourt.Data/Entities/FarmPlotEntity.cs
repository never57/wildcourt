namespace WildCourt.Data.Entities;

/// <summary>
/// 农田格种植记录（表名 farm_plots）。
/// 对应种田玩法（文档 04 §4.3/§4.4）：user_id + cell_id 唯一，记录作物与结束时间。
/// state 落库为 growing，mature 由查询时按 ready_at 计算。
/// </summary>
public class FarmPlotEntity
{
    public long UserId { get; set; }

    /// <summary>农田格（0 起，上限见 Config_YardUnlock.farmlandCount）。</summary>
    public int CellId { get; set; }

    /// <summary>作物 ID（Config_Crops.json）。</summary>
    public string CropId { get; set; } = string.Empty;

    public DateTime PlantedAt { get; set; }

    /// <summary>作物结束时间（客户端 Lua 上报，Unix 秒换算为 UTC）。</summary>
    public DateTime ReadyAt { get; set; }

    /// <summary>growing / mature（查询时计算）。</summary>
    public string State { get; set; } = "growing";

    /// <summary>已被好友偷取的数量（收获时扣除，文档 08 §8.3：剩余 70%）。</summary>
    public int StolenCount { get; set; }

    public DateTime UpdatedAt { get; set; }
}
