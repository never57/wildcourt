using UnityEngine;

namespace Game.Data
{
    /// <summary>钓鱼数据表，汇总鱼与鱼饵配置。</summary>
    [CreateAssetMenu(menuName = "Game/Data/FishingDataTable")]
    public class FishingDataTable : ScriptableObject
    {
        public FishConfig[] fishes;
        public BaitConfig[] baits;

        public FishConfig GetFish(string id)
        {
            if (fishes == null) return null;
            for (int i = 0; i < fishes.Length; i++)
                if (fishes[i] != null && fishes[i].id == id) return fishes[i];
            return null;
        }

        public BaitConfig GetBait(string id)
        {
            if (baits == null) return null;
            for (int i = 0; i < baits.Length; i++)
                if (baits[i] != null && baits[i].id == id) return baits[i];
            return null;
        }
    }
}
