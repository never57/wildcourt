using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 背包管理：物品按 itemId 计数。
/// 对应文档十五数据库 inventories 表（user_id, item_id, count）。
/// 注：加工产物（丝绸/竹片等）不可商店流通，仅自产或好友赠送（文档九 9.2 / 9.5）。
/// </summary>
public class InventoryManager
{
    private readonly Dictionary<string, int> _items = new Dictionary<string, int>();

    public IReadOnlyDictionary<string, int> Items => _items;

    public int GetCount(string itemId) => _items.TryGetValue(itemId, out var c) ? c : 0;

    public void Add(string itemId, int count)
    {
        if (count <= 0) return;
        _items[itemId] = GetCount(itemId) + count;
    }

    public bool Remove(string itemId, int count)
    {
        if (count <= 0 || GetCount(itemId) < count) return false;
        _items[itemId] -= count;
        if (_items[itemId] <= 0) _items.Remove(itemId);
        return true;
    }
}
