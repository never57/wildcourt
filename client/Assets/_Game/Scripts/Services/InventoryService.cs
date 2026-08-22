using System;
using System.Collections.Generic;
using Game.Data;
using UnityEngine;

namespace Game.Services
{
    /// <summary>
    /// 背包与货币服务：货币 + 物品（鱼饵与鱼共用 id 空间）+ 当前选中鱼饵，全部事件驱动。
    /// 纯 C# 懒加载单例，不依赖 MonoBehaviour。
    /// </summary>
    public class InventoryService
    {
        // ===== 单例 =====
        private static InventoryService _instance;
        public static InventoryService Instance => _instance ?? (_instance = new InventoryService());

        // ===== 事件 =====
        public event Action<int> OnCurrencyChanged;                 // 货币变化
        public event Action<string, int> OnItemCountChanged;        // (itemId, 新数量)
        public event Action<BaitConfig> OnSelectedBaitChanged;      // 当前鱼饵切换

        // ===== 数据 =====
        private readonly Dictionary<string, int> _items = new Dictionary<string, int>();
        private FishingDataTable _table;

        // ===== 货币 =====
        public int Currency { get; private set; }

        public bool TrySpendCurrency(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[Inventory] TrySpendCurrency 入参非法: {amount}");
                return false;
            }
            if (Currency < amount)
            {
                Debug.LogWarning($"[Inventory] 货币不足: 需要 {amount}，当前 {Currency}");
                return false;
            }
            Currency -= amount;
            OnCurrencyChanged?.Invoke(Currency);
            return true;
        }

        public void AddCurrency(int amount)
        {
            if (amount <= 0)
            {
                Debug.LogWarning($"[Inventory] AddCurrency 忽略非正数: {amount}");
                return;
            }
            Currency += amount;
            OnCurrencyChanged?.Invoke(Currency);
        }

        // ===== 物品 =====
        public int GetCount(string itemId)
        {
            if (string.IsNullOrEmpty(itemId)) return 0;
            return _items.TryGetValue(itemId, out int c) ? c : 0;
        }

        public void AddItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[Inventory] AddItem 的 itemId 为空");
                return;
            }
            if (count <= 0)
            {
                Debug.LogWarning($"[Inventory] AddItem 忽略非正数量: {count}");
                return;
            }
            int next = GetCount(itemId) + count;
            _items[itemId] = next;
            OnItemCountChanged?.Invoke(itemId, next);
        }

        public bool TryRemoveItem(string itemId, int count = 1)
        {
            if (string.IsNullOrEmpty(itemId))
            {
                Debug.LogWarning("[Inventory] TryRemoveItem 的 itemId 为空");
                return false;
            }
            if (count <= 0)
            {
                Debug.LogWarning($"[Inventory] TryRemoveItem 忽略非正数量: {count}");
                return false;
            }
            int current = GetCount(itemId);
            if (current < count)
            {
                Debug.LogWarning($"[Inventory] 物品不足: {itemId} 需要 {count}，当前 {current}");
                return false;
            }
            int next = current - count;
            if (next == 0) _items.Remove(itemId);
            else _items[itemId] = next;
            OnItemCountChanged?.Invoke(itemId, next);
            return true;
        }

        // ===== 当前鱼饵 =====
        public BaitConfig SelectedBait { get; private set; }

        public bool TrySelectBait(string baitId)
        {
            if (_table == null)
            {
                Debug.LogWarning("[Inventory] TrySelectBait 失败：尚未 Init 注入数据表");
                return false;
            }
            BaitConfig bait = _table.GetBait(baitId);
            if (bait == null)
            {
                Debug.LogWarning($"[Inventory] TrySelectBait 失败：数据表中不存在鱼饵 {baitId}");
                return false;
            }
            SelectedBait = bait;
            OnSelectedBaitChanged?.Invoke(bait);
            return true;
        }

        // ===== 初始化与调试 =====
        public void Init(FishingDataTable table)
        {
            if (table == null)
            {
                Debug.LogWarning("[Inventory] Init 传入的数据表为 null");
                return;
            }

            if (_table != null)
                Debug.LogWarning("[Inventory] Init 被重复调用，将重置为初始值");

            _table = table;
            _items.Clear();
            _items["B01"] = 6;
            Currency = 478;
            SelectedBait = table.GetBait("B01");

            OnCurrencyChanged?.Invoke(Currency);
            OnItemCountChanged?.Invoke("B01", 6);
            OnSelectedBaitChanged?.Invoke(SelectedBait);
        }

        public void LogState()
        {
            Debug.Log($"[Inventory] 货币={Currency} | B01={GetCount("B01")} | 当前鱼饵={(SelectedBait != null ? SelectedBait.displayName : "无")}");
        }
    }
}
