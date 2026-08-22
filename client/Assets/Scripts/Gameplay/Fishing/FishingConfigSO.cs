using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 钓鱼数值配置（ScriptableObject，创建于 Assets/.../Fishing/Resources 或项目中任意处）。
    /// <para>所有鱼饵/鱼类/核心规则数值均由此配置驱动，禁止硬编码进逻辑。创建入口：右键
    /// Create → WildCourt/Fishing/FishingConfig。</para>
    /// <para>若 <see cref="FishingManager"/> 未注入 Resource 实例，将回退到
    /// <see cref="CreateDefault()"/> 提供的文档默认数值，保证开箱可运行。</para>
    /// </summary>
    [CreateAssetMenu(fileName = "FishingConfig", menuName = "WildCourt/Fishing/FishingConfig")]
    public class FishingConfigSO : ScriptableObject
    {
        [Header("解锁")]
        [Tooltip("解锁钓鱼所需玩家等级（文档：等级2级，累计经验200）")]
        public int unlockLevel = 2;

        [Header("规则")]
        [Tooltip("抛竿后鱼饵生效倒计时（秒），文档固定 60s")]
        public float waitSeconds = 60f;

        [Tooltip("倒计时结束仍未咬钩则鱼饵失效：是否视为本次钓鱼结束（true）")]
        public bool expireOnTimeout = true;

        [Tooltip("切后台/退出界面超过该秒数视为中断，当前抛竿失效且鱼饵不返还")]
        public float interruptionThresholdSeconds = 10f;

        [Header("收线（连点充能QTE）")]
        [Tooltip("单次点击充能上涨量（0~1）")]
        [Range(0f, 1f)]
        public float tapGain = 0.08f;

        [Tooltip("停止点击时进度条每帧回落速率（0~1/s）")]
        [Range(0f, 1f)]
        public float drainRate = 0.25f;

        [Tooltip("总收线时长下限（秒），便于校验成功节奏是否落入 3~8s")]
        public float reelMinSeconds = 3f;

        [Tooltip("总收线时长上限（秒），超过则判定失败/脱钩")]

        public float reelMaxSeconds = 8f;

        [Header("节奏表现")]
        [Tooltip("咬钩前水花预警提前量（秒）：咬钩前1秒触发水花特效")]
        public float splashWarningSeconds = 1f;

        [Tooltip("咬钩触发的最早时间（秒，预留抛竿入水动画窗口）")]
        public float minBiteDelaySeconds = 4f;

        [Header("数据表")]
        [Tooltip("鱼饵表（B01~B03），数值严格对齐文档")]
        public List<BaitData> baits = new List<BaitData>();

        [Tooltip("鱼类表（F01~F05），数值严格对齐文档")]
        public List<FishData> fishes = new List<FishData>();

        /// <summary>按 baitId 查鱼饵。</summary>
        public BaitData GetBait(string baitId)
        {
            for (int i = 0; i < baits.Count; i++)
            {
                if (baits[i].id == baitId) return baits[i];
            }
            return null;
        }

        /// <summary>按 fishId 查鱼类。</summary>
        public FishData GetFish(string fishId)
        {
            for (int i = 0; i < fishes.Count; i++)
            {
                if (fishes[i].id == fishId) return fishes[i];
            }
            return null;
        }

        /// <summary>
        /// 生成文档默认配置（未注入配置时的兜底，数值 100% 对齐规则）。
        /// </summary>
        public static FishingConfigSO CreateDefault()
        {
            var cfg = CreateInstance<FishingConfigSO>();
            cfg.unlockLevel = 2;
            cfg.waitSeconds = 60f;
            cfg.expireOnTimeout = true;
            cfg.interruptionThresholdSeconds = 10f;
            cfg.tapGain = 0.08f;
            cfg.drainRate = 0.25f;
            cfg.reelMinSeconds = 3f;
            cfg.reelMaxSeconds = 8f;
            cfg.splashWarningSeconds = 1f;
            cfg.minBiteDelaySeconds = 4f;

            cfg.baits = new List<BaitData>
            {
                MakeBait("B01", "普通鱼饵", 2, 0.50f, ("F01", 40), ("F02", 25), ("F03", 5), ("F04", 20), ("F05", 10)),
                MakeBait("B02", "香精鱼饵", 8, 0.75f, ("F01", 30), ("F02", 25), ("F03", 10), ("F04", 15), ("F05", 20)),
                MakeBait("B03", "秘制鱼饵", 20, 0.95f, ("F01", 20), ("F02", 20), ("F03", 20), ("F04", 10), ("F05", 30)),
            };

            cfg.fishes = new List<FishData>
            {
                MakeFish("F01", "鲫鱼", FishRarity.Common, 8, true, "鲜鱼餐材料"),
                MakeFish("F02", "鲤鱼", FishRarity.Common, 12, true, "豪华套餐材料"),
                MakeFish("F03", "金鱼", FishRarity.Rare, 50, false, "不可食用，仅出售换庭贝"),
                MakeFish("F04", "草鱼", FishRarity.Common, 10, true, "鲜鱼餐材料"),
                MakeFish("F05", "鲈鱼", FishRarity.Rare, 35, true, "鲜鱼餐材料"),
            };
            return cfg;
        }

        private static BaitData MakeBait(string id, string name, int price, float biteRate, params (string, int)[] w)
        {
            var bait = new BaitData { id = id, name = name, price = price, biteRate = biteRate, weights = new List<BaitWeightEntry>() };
            for (int i = 0; i < w.Length; i++)
            {
                bait.weights.Add(new BaitWeightEntry { fishId = w[i].Item1, weight = w[i].Item2 });
            }
            return bait;
        }

        private static FishData MakeFish(string id, string name, FishRarity rarity, int sell, bool edible, string uses)
        {
            return new FishData { id = id, name = name, rarity = rarity, sellPrice = sell, edible = edible, uses = uses };
        }
    }
}