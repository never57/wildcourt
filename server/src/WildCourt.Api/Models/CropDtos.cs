namespace WildCourt.Api.Models;

/// <summary>
/// 种植请求（POST /api/v1/yard/plant）。
/// 结束时间由服务端按自身时钟计算（v1 起客户端上报的 ReadyAt 已弃用，保留字段兼容）。
/// </summary>
public class PlantCropRequest
{
    /// <summary>农田格（0 起，上限见 Config_YardUnlock.farmlandCount）。</summary>
    public int CellId { get; set; }

    /// <summary>作物 ID（Config_Crops.json）。</summary>
    public string CropId { get; set; } = string.Empty;

    /// <summary>作物结束时间（Unix 秒，仅旧版服务端使用，新版忽略）。</summary>
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

    /// <summary>服务端当前时间（Unix 秒），客户端用 readyAt - serverTime 计算剩余时间，消除时钟偏差。</summary>
    public long ServerTime { get; set; }
}

/// <summary>收获请求（POST /api/v1/yard/harvest）。</summary>
public class HarvestCropRequest
{
    public int CellId { get; set; }
}

/// <summary>收获结果（count = 作物配置 yield）。</summary>
public class HarvestResultDto
{
    public int CellId { get; set; }

    public string CropId { get; set; } = string.Empty;

    /// <summary>收获数量（= Config_Crops.yield）。</summary>
    public int Count { get; set; }
}
