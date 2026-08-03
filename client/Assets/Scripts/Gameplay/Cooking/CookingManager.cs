using UnityEngine;

namespace WildCourt.Gameplay.Cooking
{
    /// <summary>
    /// 口粮烹饪（文档 03 §3.2 Cooking/、05）：烹饪小游戏（火候 + 盐量 → 品质结算矩阵：
    /// 极佳 5 / 优秀 4 / 良好 3 / 一般 2 / 失败 0），10 食谱（Sheet6 FoodRecipes）。
    /// 成品品质在制作时写入，不做每日重算（文档 14 §14.5 第 2 项）。
    /// </summary>
    public class CookingManager : GameplayManager<CookingManager>
    {
        public enum Quality { Fail = 0, Normal = 2, Good = 3, Great = 4, Perfect = 5 }

        // TODO(文档 05)：火候/盐量 → 品质映射矩阵；10 食谱材料校验；品质分 → 好客值（avg品质×50）

        /// <summary>结算一次烹饪小游戏（由 CookingPanel / Lua UI 调用）。</summary>
        public Quality SettleCooking(int fireScore, int saltScore)
        {
            // TODO(文档 05 §5.2.1)：根据 (fireScore, saltScore) 查矩阵返回品质
            Debug.Log($"[CookingManager] 火候 {fireScore} / 盐量 {saltScore}");
            return Quality.Normal;
        }

        /// <summary>放入食物盆（容量受玩家等级约束，Sheet16 foodBowlCapacity）。</summary>
        public void PutIntoBowl(string foodId, Quality quality)
        {
            Debug.Log($"[CookingManager] 入盆 {foodId} 品质 {quality}");
        }
    }
}
