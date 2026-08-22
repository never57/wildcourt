using System;
using System.Collections.Generic;
using Game.Data;

namespace Game.Services
{
    /// <summary>
    /// 鱼篓计数服务：记录本次会话钓获的鱼（按钓获顺序），纯 C# 单例。
    /// </summary>
    public class CreelService
    {
        private static CreelService _instance;
        public static CreelService Instance => _instance ?? (_instance = new CreelService());

        private readonly List<FishConfig> _creel = new List<FishConfig>();

        /// <summary>本次会话钓获顺序。</summary>
        public IReadOnlyList<FishConfig> Creel => _creel;

        public event Action<FishConfig> OnFishAdded;

        public void Add(FishConfig fish)
        {
            if (fish == null) return;
            _creel.Add(fish);
            OnFishAdded?.Invoke(fish);
        }

        public void Clear() => _creel.Clear();
    }
}
