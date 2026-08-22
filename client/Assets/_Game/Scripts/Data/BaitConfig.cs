using System;
using UnityEngine;

namespace Game.Data
{
    /// <summary>鱼饵配置。</summary>
    [CreateAssetMenu(menuName = "Game/Data/BaitConfig")]
    public class BaitConfig : ScriptableObject
    {
        public string id;                 // B01~B03
        public string displayName;
        public int price;                 // 售价（庭贝）
        [Range(0f, 1f)] public float biteChance;      // 咬钩概率
        public int[] catchWeights = new int[5];       // 出鱼权重，顺序同 FishSpecies，和恒为100
        [TextArea] public string description;
        public Sprite icon;

        /// <summary>
        /// 加权随机出鱼，纯函数、不依赖 UnityEngine.Random，便于测试。
        /// </summary>
        public FishSpecies RollFish(System.Random rng = null)
        {
            rng = rng ?? new System.Random();
            int total = 0;
            for (int i = 0; i < catchWeights.Length; i++) total += catchWeights[i];
            if (total <= 0) return FishSpecies.Crucian;   // 权重异常兜底，避免 rng.Next(0) 抛异常
            int roll = rng.Next(total);
            int acc = 0;
            for (int i = 0; i < catchWeights.Length; i++)
            {
                acc += catchWeights[i];
                if (roll < acc) return (FishSpecies)i;
            }
            return FishSpecies.Crucian;
        }
    }
}
