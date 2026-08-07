using UnityEngine;

namespace WildCourt.Gameplay.Shop
{
    /// <summary>
    /// 商店（文档 03 §3.2 Shop/、09）：货架（种子/材料/道具/鱼饵子货架，Sheet9~12）、
    /// 双货币（庭贝 shell / 邮券 coupon，邮券仅活动产出）。种子不可自产（经济闭环，文档 05 §5.4）。
    /// </summary>
    public class ShopManager : GameplayManager<ShopManager>
    {
        /// <summary>货架类别。</summary>
        public enum ShelfCategory { Seed, Material, Item, Bait }

        // TODO(文档 09)：货架数据（GET /api/v1/shop/shelves、/bait-shelf）、购买校验与货币扣减 UI、解锁条件展示

        /// <summary>购买入口（POST /api/v1/shop/buy，currency: shell/coupon）。</summary>
        public void Buy(string itemId, int count, string currency)
        {
            Debug.Log($"[ShopManager] 购买 {itemId} x{count}（{currency}）");
            // NetManager.Instance.Post<object>(ApiPaths.ShopBuy,
            //     new BuyRequest { itemId = itemId, count = count, currency = currency },
            //     resp => { EventBus.Publish(new CurrencyChangedEvent { ... }); });
        }
    }
}
