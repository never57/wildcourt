using UnityEngine;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 概率计算模块（纯函数，无状态）。
    /// <para>精度：随机统一走项目引擎随机源 <see cref="UnityEngine.Random"/>；
    /// 概率展示保留 2 位小数（<see cref="Round2"/>）。</para>
    /// </summary>
    public static class FishingMath
    {
        /// <summary>
        /// 咬钩判定（文档：按鱼饵咬钩概率随机判定）。
        /// 输入鱼饵配置，返回是否触发咬钩。Random.value ∈ [0,1)，命中率 &lt; biteRate 即咬钩。
        /// </summary>
        public static bool RollBite(BaitData bait)
        {
            if (bait == null) return false;
            // 边界：biteRate <= 0 永不咬钩，>= 1 必咬钩（保值，等价于概率本身）。
            if (bait.biteRate <= 0f) return false;
            if (bait.biteRate >= 1f) return true;
            return Random.value < bait.biteRate;
        }

        /// <summary>
        /// 加权随机出鱼（文档：单鱼种权重/权重总和 = 出现概率）。
        /// 输入鱼饵配置，按权重表随机返回鱼类ID；权重表为空/非法时返回 null。
        /// </summary>
        public static string WeightedFish(BaitData bait)
        {
            if (bait == null || bait.weights == null || bait.weights.Count == 0) return null;

            int total = bait.TotalWeight; // 防除零见 BaitData.TotalWeight
            float roll = Random.value * total; // [0, total)
            float cum = 0f;
            for (int i = 0; i < bait.weights.Count; i++)
            {
                var entry = bait.weights[i];
                if (entry.weight <= 0) continue; // 权重为 0 的鱼种不会出现
                cum += entry.weight;
                if (roll < cum) return entry.fishId;
            }
            // 浮点舍入兜底：返回最后一个非零权重鱼种。
            for (int i = bait.weights.Count - 1; i >= 0; i--)
            {
                if (bait.weights[i].weight > 0) return bait.weights[i].fishId;
            }
            return null;
        }

        /// <summary>计算单鱼种出现概率 = weight / totalWeight，保留两位小数（用于 UI/日志展示）。</summary>
        public static float FishChance(BaitData bait, string fishId)
        {
            if (bait == null || bait.weights == null) return 0f;
            int total = bait.TotalWeight;
            for (int i = 0; i < bait.weights.Count; i++)
            {
                if (bait.weights[i].fishId == fishId)
                {
                    return Round2((float)bait.weights[i].weight / total);
                }
            }
            return 0f;
        }

        /// <summary>保留两位小数（权重概率精度要求）。</summary>
        public static float Round2(float value)
        {
            return Mathf.Round(value * 100f) / 100f;
        }
    }
}