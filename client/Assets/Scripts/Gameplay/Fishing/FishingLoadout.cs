using UnityEngine;
using WildCourt.Core;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 钓鱼装备载体（静态）：由背包界面在「确认选择鱼饵」时写入，
    /// 钓鱼主场景进入时读取。解耦背包选饵与钓鱼场景，避免在钓鱼场景内再做选饵面板。
    /// <para>业务流程：背包选鱼饵 → 写入本载体 → 跳转 FishingScene → 场景引导读取本载体。</para>
    /// </summary>
    public static class FishingLoadout
    {
        /// <summary>背包已选中的鱼饵ID（空表示未选择，钓鱼场景应拒绝开始）。</summary>
        public static string SelectedBaitId { get; private set; }

        /// <summary>背包注入的库存实例（钓鱼场景直接复用，不再自建）。</summary>
        public static InventoryManager Inventory { get; private set; }

        /// <summary>货币实例。</summary>
        public static CurrencyManager Currency { get; private set; }

        /// <summary>玩家等级实例（解锁校验用）。</summary>
        public static PlayerLevel Player { get; private set; }

        /// <summary>是否已有有效装载（背包已注入数据）。</summary>
        public static bool HasLoadout => Inventory != null;

        /// <summary>背包界面调用：写入选中鱼饵 + 注入运行时数据，随后跳转钓鱼场景。</summary>
        public static void SetLoadout(string baitId, InventoryManager inv, CurrencyManager cur, PlayerLevel pLv)
        {
            SelectedBaitId = baitId;
            Inventory = inv;
            Currency = cur;
            Player = pLv;
        }

        /// <summary>清空装载（钓鱼结束/退出场景时调用，避免脏数据残留）。</summary>
        public static void Clear()
        {
            SelectedBaitId = null;
            Inventory = null;
            Currency = null;
            Player = null;
        }

        /// <summary>当前选中的鱼饵是否仍持有（数量 &gt; 0）。</summary>
        public static bool HasSelectedBaitInStock()
        {
            if (Inventory == null || string.IsNullOrEmpty(SelectedBaitId)) return false;
            return Inventory.GetCount(SelectedBaitId) > 0;
        }
    }
}
