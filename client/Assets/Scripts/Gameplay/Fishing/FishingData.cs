using System;
using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 鱼种稀有度（文档：普通 / 稀有）。
    /// </summary>
    public enum FishRarity
    {
        Common, // 普通：鲫鱼 F01 / 鲤鱼 F02 / 草鱼 F04
        Rare,   // 稀有：金鱼 F03 / 鲈鱼 F05
    }

    /// <summary>
    /// 钓鱼有限状态机状态（待机-抛竿-等待-咬钩-收线-结算-结束）。
    /// </summary>
    public enum FishingState
    {
        Idle,       // 待机：未抛竿，可自由选饵
        Casting,    // 抛竿：已扣饵，抛竿入水动画（瞬时）
        Waiting,    // 等待：60s 生效倒计时，等待咬钩
        Hooked,     // 咬钩：触发咬钩，浮漂下沉，等待「点击收线」
        Reeling,    // 收线：连点充能进度条 QTE
        Settlement, // 结算：出鱼动画 + 结果展示
        // 结束后回到 Idle（支持「再来一次」）
    }

    /// <summary>
    /// 鱼饵配置（单行）。对应文档表 4.1：B01 普通 / B02 香精 / B03 秘制。
    /// <para>售价/咬钩率/出鱼权重均为文档强制数值，禁止在逻辑中硬编码。</para>
    /// </summary>
    [Serializable]
    public class BaitData
    {
        [Tooltip("鱼饵ID，如 B01")]
        public string id;

        [Tooltip("鱼饵名称，如 普通鱼饵")]
        public string name;

        [Tooltip("庭贝售价（仅经商店以庭贝购买，文档§)")]
        public int price;

        [Tooltip("咬钩概率（0~1，文档：B01=0.5 / B02=0.75 / B03=0.95）")]
        [Range(0f, 1f)]
        public float biteRate;

        [Tooltip("出鱼权重表：fishId + 权重（鲫鱼/鲤鱼/金鱼/草鱼/鲈鱼）")]
        public List<BaitWeightEntry> weights = new List<BaitWeightEntry>();

        /// <summary>权重总和（按需惰性缓存，避免每帧重复求和。文档：单鱼种权重/权重总和=出现概率）。</summary>
        public int TotalWeight
        {
            get
            {
                int total = 0;
                for (int i = 0; i < weights.Count; i++) total += weights[i].weight;
                return total > 0 ? total : 1; // 防除零
            }
        }
    }

    /// <summary>鱼饵→鱼种的单项出鱼权重。</summary>
    [Serializable]
    public class BaitWeightEntry
    {
        [Tooltip("鱼类ID，如 F01 鲫鱼")]
        public string fishId;

        [Tooltip("出鱼权重（整数）")]
        public int weight;
    }

    /// <summary>
    /// 鱼类配置（单行）。对应文档表 4：F01~F05。
    /// </summary>
    [Serializable]
    public class FishData
    {
        [Tooltip("鱼类ID，如 F01")]
        public string id;

        [Tooltip("名称，如 鲫鱼")]
        public string name;

        [Tooltip("稀有度")]
        public FishRarity rarity;

        [Tooltip("单条售价（庭贝）")]
        public int sellPrice;

        [Tooltip("是否可食用/作为口粮材料（金鱼为纯经济鱼种，false）")]
        public bool edible;

        [Tooltip("用途说明（鲜鱼餐/豪华套餐材料等）")]
        public string uses;
    }

    /// <summary>
    /// 一次钓鱼结算结果（供 UI 展示与事件派发）。
    /// </summary>
    public readonly struct FishingResult
    {
        /// <summary>是否成功钓获（false = 超时未咬钩 / 脱钩 / 中断）。</summary>
        public readonly bool Success;

        /// <summary>钓获鱼种配置（Success 为 true 时非空）。</summary>
        public readonly FishData Fish;

        /// <summary>结算描述（如 未咬钩 / 脱钩 / 中断）。</summary>
        public readonly string Reason;

        /// <summary>结算单条售价（庭贝）。</summary>
        public readonly int Price => Fish != null ? Fish.sellPrice : 0;

        public FishingResult(bool success, FishData fish, string reason)
        {
            Success = success;
            Fish = fish;
            Reason = reason;
        }
    }
}