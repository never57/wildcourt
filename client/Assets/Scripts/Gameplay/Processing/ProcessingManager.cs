using UnityEngine;

namespace WildCourt.Gameplay.Processing
{
    /// <summary>
    /// 加工系统（文档 03 §3.2 Processing/、04）：8 条加工配方（Sheet5 ProcessRecipes），
    /// 产物不可售不可购（经济闭环，文档 05 §5.4）。加工台交互、倒计时展示。
    /// </summary>
    public class ProcessingManager : GameplayManager<ProcessingManager>
    {
        /// <summary>加工槽状态。</summary>
        public enum SlotState { Idle, Working, Done }

        // TODO(文档 04)：8 条配方（inputItem/inputCount → outputItem/outputCount/time）、槽位上限（玩家等级，Sheet16 processorSlots）

        /// <summary>开始加工（校验材料后本地倒计时）。</summary>
        public void StartProcessing(int slotIndex, string recipeId)
        {
            Debug.Log($"[ProcessingManager] Slot {slotIndex} 开始加工 {recipeId}");
            // TODO: Config_ProcessRecipes 查配方 → 扣材料 → 倒计时 → 完成入背包
        }

        /// <summary>领取加工产物。</summary>
        public void Collect(int slotIndex)
        {
            Debug.Log($"[ProcessingManager] Slot {slotIndex} 领取产物");
        }
    }
}
