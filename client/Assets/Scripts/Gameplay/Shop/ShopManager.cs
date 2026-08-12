using System;
using UnityEngine;
using WildCourt.Lua;

namespace WildCourt.Gameplay.Shop
{
    /// <summary>
    /// 商店（文档 03 §3.2 Shop/、09）：货架（种子/材料/道具/鱼饵子货架，Sheet9~12）、
    /// 双货币（庭贝 shell / 邮券 coupon，邮券仅活动产出）。种子不可自产（经济闭环，文档 05 §5.4）。
    /// <para>
    /// 本类为静态"薄壳"：纯逻辑（分类过滤 / 购买可行性判定）下沉到
    /// <c>Logic.Shop.ShopManager</c>（热更 Lua），网络上报与余额刷新留在 C#。
    /// 购买动作涉及网络回调与 UI 状态，不适合下沉；预加载链路
    /// （LoginController 注入 YardPanelPrefab）保持 C# 不动，不依赖 Lua。
    /// </para>
    /// </summary>
    public static class ShopManager
    {
        /// <summary>热更 Lua 逻辑层是否已就绪（provider 注入成功）。桩模式 / 模块缺失时为 false。</summary>
        private static bool _luaReady;

        /// <summary>provider 是否已尝试注入（惰性，首次调用时触发）。</summary>
        private static bool _injected;

        /// <summary>
        /// 分类过滤（热更 Lua）：targetCategory 为 "全部"/空 时放行，
        /// 否则需与商品 category 精确匹配（委托 Lua 的 MatchCategory 判定）。
        /// </summary>
        public static bool FilterByCategory(string itemCategory, string targetCategory)
        {
            EnsureInjected();
            if (_luaReady)
            {
                return LuaManager.Instance.CallFunc<bool>(
                    "Logic.Shop.ShopManager", "MatchCategory", itemCategory, targetCategory);
            }
            // 桩模式回退：与 Lua 规则一致
            if (targetCategory == null || targetCategory == string.Empty || targetCategory == "全部")
                return true;
            return itemCategory == targetCategory;
        }

        /// <summary>
        /// 购买可行性判定（热更 Lua）：owned 背包拥有量 + maxCount 拥有上限 + placed 已摆放数。
        /// 剩余 = maxCount - owned；<= 0 不可再购。
        /// </summary>
        public static bool CanBuy(int owned, int maxCount, int placed)
        {
            EnsureInjected();
            if (_luaReady)
            {
                return LuaManager.Instance.CallFunc<bool>(
                    "Logic.Shop.ShopManager", "CanBuy", owned, maxCount, placed);
            }
            // 桩模式回退：与 Lua 规则一致
            if (maxCount <= 0) return false;
            return maxCount - owned > 0;
        }

        /// <summary>惰性注入 provider（首次调用纯逻辑前确保 Lua 就绪）。</summary>
        private static void EnsureInjected()
        {
            if (_injected) return;
            _injected = true;
            InjectLuaProvider();
        }

        /// <summary>
        /// 把购买上报能力注入 <c>Logic.Shop.ShopManager</c>（热更 Lua 的 provider）。
        /// 桩模式（未启用 USE_XLUA）下跳过。
        /// </summary>
        private static void InjectLuaProvider()
        {
#if USE_XLUA
            try
            {
                var env = LuaEnvHolder.Env;
                if (env == null || LuaManager.Instance == null) return;
                var provider = env.NewTable();
                // 预留：若未来把购买请求构造也下沉 Lua，可在此注入 sendBuy。
                LuaManager.Instance.CallFunc<bool>(
                    "Logic.Shop.ShopManager", "SetDataProvider", provider);
                _luaReady = true;
                Debug.Log("[ShopManager] Lua 逻辑层 provider 注入成功");
            }
            catch (Exception e)
            {
                _luaReady = false;
                Debug.LogWarning($"[ShopManager] Lua provider 注入失败（回退 C# 逻辑）: {e.Message}");
            }
#endif
        }
    }
}
