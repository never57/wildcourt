using System;

namespace WildCourt.Net.Protocol
{
    // ---------- Shop（文档 02 §2.2 ShopController、09） ----------

    /// <summary>货架商品（Sheet9~12 Shop 行）。</summary>
    [Serializable]
    public class ShelfItem
    {
        public string id;
        public string itemName;
        public string category;   // seed / material / item / bait
        public int buyPrice;
        public int sellPrice;
        public string unlockCond;
        public string effect;
    }

    [Serializable]
    public class ShelfResponse
    {
        public ShelfItem[] items;
    }

    /// <summary>购买请求（currency: shell / coupon，文档 09 §9.1.1 双货币）。</summary>
    [Serializable]
    public class BuyRequest
    {
        public string itemId;
        public int count;
        public string currency;
    }

    // ---------- Yard（YardController、文档 03 §3.2） ----------

    [Serializable]
    public class YardResponse
    {
        public int level;
        public string layoutJson;
    }

    /// <summary>保存布局（3 个布局槽，slotIndex 0~2；服务端校验等级容量，文档 02 §2.3 YardService）。</summary>
    [Serializable]
    public class LayoutSaveRequest
    {
        public string layoutJson;
        public int slotIndex;
    }
}
