using UnityEngine;

namespace WildCourt.Gameplay.Crop
{
    /// <summary>
    /// 农业生产（文档 03 §3.2 Crop/）：种植、作物状态机（生长→成熟→收获）、钓鱼。
    /// 数值来自 Config_Crops.json / Config_Fish.json / Config_Bait.json（Sheet3/4/4.1）。
    /// 服务端为权威，本管理器负责本地表现与倒计时展示。
    /// </summary>
    public class CropManager : GameplayManager<CropManager>
    {
        /// <summary>农田格状态：空闲 / 生长中（剩余秒） / 成熟 / 收获倒计时。</summary>
        public enum CellState { Empty, Growing, Mature, Harvested }

        // TODO(文档 04)：农田格数据（cellId → 作物/剩余时间）、种植校验（种子来源：商店/好友/签到，不可自产）
        // TODO(文档 04 §4.5)：钓鱼 —— 鱼饵 B01~B03 咬钩率 50/75/95%，出鱼权重驱动

        /// <summary>种植入口（POST /api/v1/yard/layout 联动；服务端校验）。</summary>
        public void Plant(string cellId, string seedId)
        {
            Debug.Log($"[CropManager] Plant {cellId} <- {seedId}");
            // TODO: 本地倒计时 + 成熟通知；服务端权威结算
        }

        /// <summary>收获成熟作物。</summary>
        public void Harvest(string cellId)
        {
            Debug.Log($"[CropManager] Harvest {cellId}");
            // TODO(文档 08 §8.3)：好友可偷菜 30% 产量、保护罩 24h
        }
    }
}
