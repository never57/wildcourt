using System;

namespace WildCourt.Net.Protocol
{
    // ---------- Yard / Farm（文档 02 §2.2 YardController、文档 04 种田） ----------

    /// <summary>种植请求（POST /api/v1/yard/plant）。readyAt 为 Unix 秒；新版服务端按自身时钟权威计算，忽略本字段。</summary>
    [Serializable]
    public class PlantCropRequest
    {
        public int cellId;      // 农田格（0 起，上限见 Config_YardUnlock.farmlandCount）
        public string cropId;   // 作物 ID（Config_Crops.json，如 C01）
        public long readyAt;    // 作物结束时间（Unix 秒，仅旧版服务端使用）
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
        public long serverTime; // 服务端当前时间（Unix 秒）：剩余时间 = readyAt - serverTime，消除客户端/服务端时钟偏差
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
