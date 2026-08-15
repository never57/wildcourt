using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;

/// <summary>
/// 全屏商店主页：把 goodsCard 预制体批量生成到 ScrollRect 的 Content 里，
/// 由 Content 上的 GridLayoutGroup 自动排版，滚轮 / 手指滑动即可浏览管理所有卡片。
///
/// 使用步骤：
/// 1. 做一个全屏 Panel（锚点拉伸到四角）作为商店主页，挂上本脚本。
/// 2. Panel 下放一个 ScrollRect（含 Viewport + Content），Content 上挂 GridLayoutGroup。
/// 3. Inspector 里把 goodsCard 预制体拖到 Card Prefab，把 Content 拖到 Content。
/// 4. 在 Items 里填商品数据（名称、图标、单价）。
/// </summary>
public class ShopPage : MonoBehaviour
{
    [Serializable]
    public class ShopItem
    {
        [Tooltip("商品名")]
        public string goodsName = "商品";

        [Tooltip("服务器商品 ID（服务器模式购买用）")]
        public string itemId = "";

        [Tooltip("商品图标（可为空）")]
        public Sprite icon;

        [Tooltip("单价")]
        public float unitPrice = 10f;

        [Tooltip("剩余可购买数量")]
        public int remaining = 10;

        [Tooltip("启用剩余可购买限制（关掉后不限制剩余，只受货币限制）")]
        public bool useRemainingLimit = true;

        [Tooltip("已拥有数量")]
        public int owned = 0;
    }

    [Header("引用")]
    [Tooltip("商品卡预制体（prefeb/goodsCard）")]
    [SerializeField] private GameObject cardPrefab;

    [Tooltip("ScrollRect 的 Content（上面要挂 GridLayoutGroup）")]
    [SerializeField] private RectTransform content;

    [Header("货币")]
    [Tooltip("玩家当前拥有的货币数量，决定每张卡最多能买几件")]
    [SerializeField] private float currencyAmount = 100f;

    [Tooltip("屏幕上显示货币数量的文本（不拖会自动找名字带 currency / 货币 / coin 的文本）")]
    [SerializeField] private TMP_Text currencyText;

    [Header("显示")]
    [Tooltip("游戏开始时商店页是否隐藏（防止全屏面板挡住其它 UI 的点击）")]
    [SerializeField] private bool startHidden = true;

    [Header("商品数据")]
    [SerializeField] private List<ShopItem> items = new List<ShopItem>();

    [Header("服务器模式（种子商店等货架）")]
    [Tooltip("开启后从服务器货架加载商品，购买走服务器接口")]
    [SerializeField] private bool serverMode;

    [Tooltip("货架类别过滤（seed/material/item/bait；留空不过滤）")]
    [SerializeField] private string shelfCategory = "seed";

    [Tooltip("购买货币：shell（庭贝）/ coupon（邮券）")]
    [SerializeField] private string shelfCurrency = "shell";

    /// <summary>服务器模式：货架加载中（防止重复请求）</summary>
    private bool _shelfLoading;

    /// <summary>已生成的卡片，和 items 一一对应（不依赖 Content 子物体序号，更稳）</summary>
    private readonly List<GoodsCard> spawnedCards = new List<GoodsCard>();

    /// <summary>数据快照：自动检测「数据变了 → 刷新卡片」</summary>
    private readonly List<ShopItemSnapshot> snapshots = new List<ShopItemSnapshot>();

    /// <summary>商品原始顺序（卖完沉底后、剩余恢复时回到原位用）</summary>
    private readonly List<ShopItem> baseOrder = new List<ShopItem>();
    private float nextAutoRefresh = 0f;
    private const float AutoRefreshInterval = 0.25f;

    [Serializable]
    private struct ShopItemSnapshot
    {
        public string goodsName;
        public float unitPrice;
        public int remaining;
        public int owned;
        public bool useRemainingLimit;

        public static ShopItemSnapshot From(ShopItem item)
        {
            return new ShopItemSnapshot
            {
                goodsName = item.goodsName,
                unitPrice = item.unitPrice,
                remaining = item.remaining,
                owned = item.owned,
                useRemainingLimit = item.useRemainingLimit
            };
        }

        public bool EqualsItem(ShopItem item)
        {
            return goodsName == item.goodsName
                && unitPrice == item.unitPrice
                && remaining == item.remaining
                && owned == item.owned
                && useRemainingLimit == item.useRemainingLimit;
        }
    }

    private void Start()
    {
        AutoFindCurrencyText();
        if (serverMode)
        {
            // 服务器模式：数据来自货架接口（Open() 时也会刷新）
            LoadFromServer();
        }
        else
        {
            Rebuild();
            RefreshCurrencyDisplay();
        }

        // 防止全屏商店页挡住其它 UI：默认隐藏，需要时用 Open() 打开
        if (startHidden) gameObject.SetActive(false);
    }

    /// <summary>每 0.25 秒检测一次数据是否被改动（Inspector 或代码改了都算），改了自动刷新卡片。</summary>
    private void Update()
    {
        if (serverMode) return;   // 服务器模式数据不受 Inspector 编辑影响，无需轮询
        if (Time.time < nextAutoRefresh) return;
        nextAutoRefresh = Time.time + AutoRefreshInterval;

        for (int i = items.Count - 1; i >= 0; i--)
        {
            if (i >= snapshots.Count) continue;
            if (!snapshots[i].EqualsItem(items[i]))
            {
                snapshots[i] = ShopItemSnapshot.From(items[i]);
                RefreshItem(i);
                ReorderByBaseOrder();
            }
        }
    }

    /// <summary>重建数据快照（生成 / 清空卡片后调用）</summary>
    private void RebuildSnapshots()
    {
        snapshots.Clear();
        foreach (ShopItem item in items)
            snapshots.Add(ShopItemSnapshot.From(item));
    }

    /// <summary>清空 Content 并按 Items 重新生成所有卡片（Inspector 右键可调用）</summary>
    [ContextMenu("重建列表")]
    public void Rebuild()
    {
        if (!CheckRefs()) return;

        ClearAll();
        foreach (ShopItem item in items)
            SpawnCard(item);
        RebuildSnapshots();
        RebuildBaseOrder();
    }

    /// <summary>运行时新增一张卡（例如购买解锁后调用）</summary>
    public void AddItem(string goodsName, Sprite icon, float unitPrice, int remaining = 10, int owned = 0, bool useRemainingLimit = true)
    {
        if (!CheckRefs()) return;
        ShopItem item = new ShopItem
        {
            goodsName = goodsName,
            icon = icon,
            unitPrice = unitPrice,
            remaining = remaining,
            owned = owned,
            useRemainingLimit = useRemainingLimit
        };
        items.Add(item);   // 数据先进列表，保证序号对齐
        SpawnCard(item);
        snapshots.Add(ShopItemSnapshot.From(item));
        baseOrder.Add(item);
    }

    /// <summary>删除最后一张卡</summary>
    public void RemoveLast()
    {
        if (spawnedCards.Count == 0) return;
        GoodsCard last = spawnedCards[spawnedCards.Count - 1];
        spawnedCards.RemoveAt(spawnedCards.Count - 1);
        if (last != null) Destroy(last.gameObject);
        if (items.Count > 0)
        {
            ShopItem removed = items[items.Count - 1];
            items.RemoveAt(items.Count - 1);   // 数据同步删掉
            baseOrder.Remove(removed);
        }
        if (snapshots.Count > items.Count) snapshots.RemoveAt(snapshots.Count - 1);
    }

    /// <summary>按「未卖完在前、卖完沉底、各自保持原始顺序」重排所有卡片。</summary>
    private void ReorderByBaseOrder()
    {
        if (items.Count != baseOrder.Count)
            RebuildBaseOrder();

        List<ShopItem> order = new List<ShopItem>(items);
        order.Sort((a, b) =>
        {
            bool aSold = a.useRemainingLimit && a.remaining <= 0;
            bool bSold = b.useRemainingLimit && b.remaining <= 0;
            if (aSold != bSold) return aSold ? 1 : -1;   // 卖完的放后面
            return baseOrder.IndexOf(a).CompareTo(baseOrder.IndexOf(b));
        });

        // 顺序没变就不用动
        bool same = true;
        for (int i = 0; i < order.Count; i++)
        {
            if (!ReferenceEquals(order[i], items[i])) { same = false; break; }
        }
        if (same) return;

        // 按新顺序重排 items / spawnedCards / snapshots
        List<GoodsCard> newCards = new List<GoodsCard>(order.Count);
        List<ShopItemSnapshot> newSnaps = new List<ShopItemSnapshot>(order.Count);
        for (int i = 0; i < order.Count; i++)
        {
            int old = items.IndexOf(order[i]);
            newCards.Add(old >= 0 && old < spawnedCards.Count ? spawnedCards[old] : null);
            newSnaps.Add(old >= 0 && old < snapshots.Count ? snapshots[old] : default);
        }

        items.Clear();
        items.AddRange(order);
        spawnedCards.Clear();
        spawnedCards.AddRange(newCards);
        snapshots.Clear();
        snapshots.AddRange(newSnaps);

        // 卡片层级 = 显示顺序
        for (int i = 0; i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
                spawnedCards[i].transform.SetSiblingIndex(i);
        }
    }

    /// <summary>重建原始顺序（生成 / 清空卡片后调用）</summary>
    private void RebuildBaseOrder()
    {
        baseOrder.Clear();
        foreach (ShopItem item in items)
            baseOrder.Add(item);
    }

    /// <summary>清空所有卡</summary>
    public void ClearAll()
    {
        spawnedCards.Clear();
        if (content == null) return;
        for (int i = content.childCount - 1; i >= 0; i--)
            Destroy(content.GetChild(i).gameObject);
    }

    /// <summary>打开商店主页（显示整个面板）</summary>
    public void Open()
    {
        gameObject.SetActive(true);
        if (serverMode) LoadFromServer();   // 每次打开刷新货架与余额
        RefreshCurrencyDisplay();
        RefreshAllQuantityCaps();
    }

    /// <summary>关闭商店主页（隐藏整个面板，回到游戏）</summary>
    public void Close()
    {
        gameObject.SetActive(false);
    }

    /// <summary>设置玩家货币数量，并立即刷新所有卡片可购买上限（购买后扣钱时调用）</summary>
    public void SetCurrency(float amount)
    {
        currencyAmount = amount;
        RefreshCurrencyDisplay();
        RefreshAllQuantityCaps();
    }

    /// <summary>刷新所有卡片的可购买上限（购买扣钱 / 改钱后调用）</summary>
    private void RefreshAllQuantityCaps()
    {
        for (int i = 0; i < items.Count && i < spawnedCards.Count; i++)
        {
            if (spawnedCards[i] != null)
                ApplyCardRange(spawnedCards[i], items[i]);
        }
    }

    /// <summary>运行时切换某件商品（按 Items 序号）的「剩余可购买限制」，立即生效。</summary>
    public void SetRemainingLimit(int itemIndex, bool enable)
    {
        if (itemIndex < 0 || itemIndex >= items.Count) return;
        items[itemIndex].useRemainingLimit = enable;

        if (itemIndex < spawnedCards.Count && spawnedCards[itemIndex] != null)
        {
            GoodsCard card = spawnedCards[itemIndex];
            card.SetRemainingVisible(enable);
            card.SetRemaining(items[itemIndex].remaining);
            ApplyCardRange(card, items[itemIndex]);
        }
    }

    /// <summary>运行时给所有商品统一切换「剩余可购买限制」，立即生效。</summary>
    public void SetAllRemainingLimits(bool enable)
    {
        for (int i = 0; i < items.Count; i++)
            SetRemainingLimit(i, enable);
    }

    /// <summary>查询某件商品当前的「剩余可购买限制」开关状态。</summary>
    public bool GetRemainingLimit(int itemIndex)
    {
        return itemIndex >= 0 && itemIndex < items.Count && items[itemIndex].useRemainingLimit;
    }

    /// <summary>用 Items 数据重新刷新某张卡（直接改了数据后调用，立即同步到卡片）。</summary>
    public void RefreshItem(int itemIndex)
    {
        if (itemIndex < 0 || itemIndex >= items.Count || itemIndex >= spawnedCards.Count)
        {
            Debug.LogWarning($"ShopPage.RefreshItem({itemIndex}) 失败：数据 {items.Count} 条，卡片 {spawnedCards.Count} 张，请先 Rebuild()", this);
            return;
        }

        GoodsCard card = spawnedCards[itemIndex];
        if (card == null) return;

        ShopItem item = items[itemIndex];
        card.Setup(item.goodsName, item.icon, item.unitPrice);
        card.SetRemaining(item.remaining);
        card.SetRemainingVisible(item.useRemainingLimit);
        card.SetOwned(item.owned);
        ApplyCardRange(card, item);
    }

    /// <summary>改数据 + 自动刷新卡片：商品名</summary>
    public void SetItemName(int itemIndex, string goodsName)
    {
        if (!InRange(itemIndex)) return;
        items[itemIndex].goodsName = goodsName;
        RefreshItem(itemIndex);
    }

    /// <summary>改数据 + 自动刷新卡片：图标</summary>
    public void SetItemIcon(int itemIndex, Sprite icon)
    {
        if (!InRange(itemIndex)) return;
        items[itemIndex].icon = icon;
        RefreshItem(itemIndex);
    }

    /// <summary>改数据 + 自动刷新卡片：单价</summary>
    public void SetItemPrice(int itemIndex, float unitPrice)
    {
        if (!InRange(itemIndex)) return;
        items[itemIndex].unitPrice = unitPrice;
        RefreshItem(itemIndex);
    }

    /// <summary>改数据 + 自动刷新卡片：剩余可购买数量</summary>
    public void SetItemRemaining(int itemIndex, int remaining)
    {
        if (!InRange(itemIndex)) return;
        items[itemIndex].remaining = remaining;
        RefreshItem(itemIndex);
    }

    /// <summary>改数据 + 自动刷新卡片：已拥有数量</summary>
    public void SetItemOwned(int itemIndex, int owned)
    {
        if (!InRange(itemIndex)) return;
        items[itemIndex].owned = owned;
        RefreshItem(itemIndex);
    }

    /// <summary>序号是否在 Items 范围内</summary>
    private bool InRange(int itemIndex)
    {
        return itemIndex >= 0 && itemIndex < items.Count;
    }

    /// <summary>可购买上限 = min(剩余可购买数量, 货币够买几件)</summary>
    private int ComputeMaxQuantity(ShopItem item)
    {
        int byMoney = item.unitPrice > 0f
            ? Mathf.FloorToInt(currencyAmount / item.unitPrice)
            : int.MaxValue;
        return item.useRemainingLimit ? Mathf.Min(item.remaining, byMoney) : byMoney;
    }

    /// <summary>应用滑块范围：不可购买（卖完 / 钱不够一件）→ 下限 0；可购买 → 下限 1（默认）。</summary>
    private void ApplyCardRange(GoodsCard card, ShopItem item)
    {
        if (card == null) return;
        int max = ComputeMaxQuantity(item);
        bool soldOut = item.useRemainingLimit && item.remaining <= 0;
        bool cannotAfford = item.unitPrice > 0f && currencyAmount < item.unitPrice;
        card.SetQuantityRange(soldOut || cannotAfford ? 0 : 1, max);
    }

    /// <summary>购买逻辑：扣钱、已拥有增加、剩余减少、刷新上限</summary>
    private void OnBuy(GoodsCard card, ShopItem item, int quantity)
    {
        if (quantity <= 0) return;

        if (serverMode)
        {
            // 服务器模式：余额与库存以服务器为准，走 /shop/buy
            RequestServerBuy(card, item, quantity);
            return;
        }

        float cost = item.unitPrice * quantity;
        if (item.useRemainingLimit && item.remaining < quantity)
        {
            Debug.Log($"「{item.goodsName}」剩余不足（还剩 {item.remaining}）", this);
            return;
        }
        if (cost > currencyAmount)
        {
            Debug.Log($"「{item.goodsName}」货币不足（需要 {cost}，现有 {currencyAmount}）", this);
            return;
        }

        currencyAmount -= cost;
        item.owned += quantity;
        if (item.useRemainingLimit) item.remaining -= quantity;

        // 剩余跌到 0 → 沉底；剩余恢复 → 回到原位（ReorderByBaseOrder 统一处理）
        ReorderByBaseOrder();

        card.ResetQuantity();
        card.SetOwned(item.owned);
        card.SetRemaining(item.remaining);
        RefreshAllQuantityCaps();
        RefreshCurrencyDisplay();

        Debug.Log($"已购买「{item.goodsName}」x{quantity}，花费 {cost}，剩余货币 {currencyAmount}，剩余库存 {item.remaining}（限制开关={item.useRemainingLimit}）", this);
    }

    // ---------- 服务器模式（种子商店等货架，文档 09） ----------

    /// <summary>服务器模式：拉取货架（按 shelfCategory 过滤）并重建商品列表。</summary>
    public void LoadFromServer()
    {
        var net = NetManager.Instance;
        if (net == null || _shelfLoading) return;

        _shelfLoading = true;
        net.Get<ShelfItem[]>(ApiPaths.ShopShelves,
            resp =>
            {
                _shelfLoading = false;
                ApplyShelves(resp.data);
            },
            (code, msg) =>
            {
                _shelfLoading = false;
                Debug.LogWarning($"[ShopPage] 货架加载失败：{msg}", this);
            });
    }

    /// <summary>把货架数据写入商品列表并重建卡片（单价大于 0 才上架）。</summary>
    private void ApplyShelves(ShelfItem[] shelves)
    {
        items.Clear();
        if (shelves != null)
        {
            foreach (ShelfItem s in shelves)
            {
                if (s == null) continue;
                if (!string.IsNullOrEmpty(shelfCategory)
                    && !s.category.Equals(shelfCategory, StringComparison.OrdinalIgnoreCase))
                    continue;
                if (s.buyPrice <= 0) continue;

                items.Add(new ShopItem
                {
                    itemId = s.id,
                    goodsName = s.itemName,
                    icon = null,   // 种子图标资源暂缺（Resources/Art/Sprite/Crops 后续补充）
                    unitPrice = s.buyPrice,
                    remaining = 0,
                    owned = 0,
                    useRemainingLimit = false,   // 种子无库存上限，只受庭贝约束
                });
            }
        }

        Rebuild();
        RefreshCurrencyDisplay();

        // 拉背包刷新「已拥有」，拉余额刷新可购买上限
        LoadInventory();
        LoadBalance();
    }

    /// <summary>服务器模式：拉取玩家余额（庭贝 shell），刷新货币显示与购买上限。</summary>
    private void LoadBalance()
    {
        var net = NetManager.Instance;
        if (net == null) return;
        net.Get<UserProfileResponse>(ApiPaths.UserProfile,
            resp =>
            {
                if (resp.data == null) return;
                currencyAmount = resp.data.shell;
                RefreshCurrencyDisplay();
                RefreshAllQuantityCaps();
            },
            (code, msg) => Debug.LogWarning($"[ShopPage] 余额加载失败：{msg}", this));
    }

    /// <summary>服务器模式：拉取背包，刷新每张卡的「已拥有」数量。</summary>
    private void LoadInventory()
    {
        var net = NetManager.Instance;
        if (net == null) return;
        net.Get<InventoryResponse>(ApiPaths.UserInventory,
            resp =>
            {
                var ownedMap = new Dictionary<string, int>();
                if (resp.data != null && resp.data.items != null)
                {
                    foreach (InventoryItem it in resp.data.items)
                    {
                        if (it != null) ownedMap[it.itemId] = it.count;
                    }
                }
                for (int i = 0; i < items.Count && i < spawnedCards.Count; i++)
                {
                    ShopItem item = items[i];
                    if (item == null || spawnedCards[i] == null) continue;
                    item.owned = ownedMap.TryGetValue(item.itemId, out int count) ? count : 0;
                    spawnedCards[i].SetOwned(item.owned);
                }
            },
            (code, msg) => Debug.LogWarning($"[ShopPage] 背包加载失败：{msg}", this));
    }

    /// <summary>服务器模式购买：POST /shop/buy（货币 = shelfCurrency，庭贝 shell），成功后用服务器余额刷新并广播货币变化。</summary>
    private void RequestServerBuy(GoodsCard card, ShopItem item, int quantity)
    {
        if (quantity <= 0 || string.IsNullOrEmpty(item.itemId)) return;

        var net = NetManager.Instance;
        if (net == null) return;

        net.Post<BuyResponse>(ApiPaths.ShopBuy,
            new BuyRequest { itemId = item.itemId, count = quantity, currency = shelfCurrency },
            resp =>
            {
                if (resp.data == null) return;
                // 以服务器返回的权威余额为准（庭贝）
                currencyAmount = resp.data.shell;
                item.owned += quantity;
                card.ResetQuantity();
                card.SetOwned(item.owned);
                RefreshAllQuantityCaps();
                RefreshCurrencyDisplay();

                // 广播货币变化：YardMainUI 等订阅方会重拉 profile 对齐余额
                EventBus.Publish(new CurrencyChangedEvent
                {
                    Shell = resp.data.shell,
                    Coupon = resp.data.coupon,
                });

                Debug.Log($"已购买「{item.goodsName}」x{quantity}，剩余庭贝 {resp.data.shell}", this);
            },
            (code, msg) => Debug.LogWarning($"购买「{item.goodsName}」失败：{msg}", this));
    }

    /// <summary>刷新屏幕上显示的货币文本</summary>
    private void RefreshCurrencyDisplay()
    {
        if (currencyText == null) return;
        currencyText.text = currencyAmount % 1f == 0f
            ? currencyAmount.ToString("0")
            : currencyAmount.ToString("0.##");
    }

    /// <summary>没拖引用时，自动找名字带 currency / 货币 / coin 的文本</summary>
    private void AutoFindCurrencyText()
    {
        if (currencyText != null) return;
        foreach (TMP_Text t in GetComponentsInChildren<TMP_Text>(true))
        {
            string n = t.gameObject.name;
            if (n.Contains("currency") || n.Contains("Currency") || n.Contains("货币") || n.Contains("coin") || n.Contains("Coin"))
            {
                currencyText = t;
                return;
            }
        }
    }

    private void SpawnCard(ShopItem item)
    {
        GameObject go = Instantiate(cardPrefab, content);
        go.name = item.goodsName;

        GoodsCard card = go.GetComponent<GoodsCard>();
        if (card != null)
        {
            spawnedCards.Add(card);   // 记录卡片，和 items 一一对应
            card.Setup(item.goodsName, item.icon, item.unitPrice);
            ApplyCardRange(card, item);
            card.SetRemaining(item.remaining);
            card.SetRemainingVisible(item.useRemainingLimit);
            card.SetOwned(item.owned);
            card.onBuy.AddListener(qty => OnBuy(card, item, qty));
        }
    }

    private bool CheckRefs()
    {
        if (cardPrefab == null || content == null)
        {
            Debug.LogWarning("ShopPage: 请先在 Inspector 拖入 Card Prefab 和 Content", this);
            return false;
        }
        return true;
    }
}
