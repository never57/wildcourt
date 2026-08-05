using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 装饰静态图加载（交互优化）：Resources/Decorations/{id}.png。
    /// 命中缓存避免重复 Resources.Load；缺失记录后不重复加载，调用方回退占位色块。
    /// </summary>
    public static class DecorationSprites
    {
        private static readonly Dictionary<string, Sprite> Cache = new();
        private static readonly HashSet<string> Missed = new();

        public static Sprite Get(string id)
        {
            if (string.IsNullOrEmpty(id)) return null;
            if (Cache.TryGetValue(id, out var sprite)) return sprite;
            if (Missed.Contains(id)) return null;

            var loaded = Resources.Load<Sprite>("Decorations/" + id);
            if (loaded != null) Cache[id] = loaded;
            else Missed.Add(id);
            return loaded;
        }

        /// <summary>清空缓存（重载资源/热更后调用）。</summary>
        public static void Clear()
        {
            Cache.Clear();
            Missed.Clear();
        }
    }
}
