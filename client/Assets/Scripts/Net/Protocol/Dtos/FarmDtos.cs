using System;

namespace WildCourt.Net.Protocol
{
    // ---------- Yard / Farm（文档 02 §2.2 YardController、文档 04 种田） ----------

    /// <summary>种植请求（POST /api/v1/yard/plant）。readyAt 为 Unix 秒，客户端按 now + growTime*60 计算。</summary>
    [Serializable]
    public class PlantCropRequest
    {
        public int cellId;      // 农田格（0 起，上限见 Config_YardUnlock.farmlandCount）
        public string cropId;   // 作物 ID（Config_Crops.json，如 C01）
        public long readyAt;    // 作物结束时间（Unix 秒）
    }

    /// <summary>农田格种植状态（信封 data）。</summary>
    [Serializable]
    public class FarmPlotDto
    {
        public int cellId;
        public string cropId;
        public long readyAt;    // Unix 秒
        public string state;    // growing / mature
    }

    /// <summary>种植状态列表（GET /api/v1/yard/plots，信封 data）。</summary>
    [Serializable]
    public class FarmPlotsResponse
    {
        public FarmPlotDto[] plots;
    }

    /// <summary>收获请求（POST /api/v1/yard/harvest）。</summary>
    [Serializable]
    public class HarvestCropRequest
    {
        public int cellId;
    }

    /// <summary>收获结果（count = 作物配置 yield - 被偷数量，已入背包）。</summary>
    [Serializable]
    public class HarvestResultDto
    {
        public int cellId;
        public string cropId;
        public int count;
    }
}
