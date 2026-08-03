using UnityEngine;

namespace WildCourt.Gameplay.Match
{
    /// <summary>
    /// 漫游匹配（文档 03 §3.2 Match/、07 §7.7/7.8）：派遣目的地选择、匹配结果展示。
    /// 权重 60% 好客值 / 20% 随机 / 10% 首访 / 10% 好友；算法 Lua 复用（Logic/Match/MatchMaker.lua，服务端同源）。
    /// </summary>
    public class MatchManager : GameplayManager<MatchManager>
    {
        // TODO(文档 07)：目的地选择（省/市，Config_Provinces/Config_Cities）、匹配结果列表展示、NPC 补位庭院

        /// <summary>本地调用 MatchMaker（客户端预览；正式匹配由服务端 MatchService 执行）。</summary>
        public void FindTargets(int provinceId, int cityId, int count)
        {
            // var targets = WildCourt.Lua.LuaManager.Instance.CallFunc<object>("Logic.Match.MatchMaker", "FindTargets",
            //     provinceId, cityId, count, null);
            Debug.Log($"[MatchManager] 匹配 省 {provinceId} 市 {cityId} 目标 {count} 个");
        }
    }
}
