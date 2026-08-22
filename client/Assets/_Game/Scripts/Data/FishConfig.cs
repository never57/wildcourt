using UnityEngine;

namespace Game.Data
{
    /// <summary>鱼种枚举，顺序即权重表顺序。</summary>
    public enum FishSpecies { Crucian, Carp, Goldfish, GrassCarp, Bass }

    /// <summary>稀有度。</summary>
    public enum Rarity { Normal, Rare }

    /// <summary>鱼配置。</summary>
    [CreateAssetMenu(menuName = "Game/Data/FishConfig")]
    public class FishConfig : ScriptableObject
    {
        public string id;          // F01~F05
        public FishSpecies species;
        public string displayName;
        public Rarity rarity;
        public int sellPrice;      // 售价（庭贝）
        [TextArea] public string usage;
        public Sprite icon;        // 占位图
    }
}
