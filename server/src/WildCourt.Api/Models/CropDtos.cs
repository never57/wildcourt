namespace WildCourt.Api.Models;

/// <summary>
/// 种植请求（POST /api/v1/yard/plant）。
/// 结束时间由客户端 Lua（FarmManager）按 now + growTime*60 计算后上报，v1 信任该值。
/// </summary>
public class PlantCropRequest
{
    /// <summary>农田格（0 起，上限见 Config_YardUnlock.farmlandCount）。</summary>
    public int CellId { get; set; }

    /// <summary>作物 ID（Config_Crops.json）。</summary>
    public string CropId { get; set; } = string.Empty;

    /// <summary>作物结束时间（Unix 秒）。</summary>
    public long ReadyAt { get; set; }
}

/// <summary>农田格种植状态。</summary>
public class FarmPlotDto
{
    public int CellId { get; set; }

    public string CropId { get; set; } = string.Empty;

    /// <summary>作物结束时间（Unix 秒）。</summary>
    public long ReadyAt { get; set; }

    /// <summary>growing / mature（mature 表示 readyAt 已过，可收获）。</summary>
    public string State { get; set; } = string.Empty;
}

/// <summary>种植状态列表（GET /api/v1/yard/plots）。</summary>
public class FarmPlotsResponse
{
    public List<FarmPlotDto> Plots { get; set; } = new();
}
