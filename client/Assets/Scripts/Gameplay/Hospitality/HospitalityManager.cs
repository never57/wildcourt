using UnityEngine;

namespace WildCourt.Gameplay.Hospitality
{
    /// <summary>
    /// 好客值（文档 03 §3.2 Hospitality/、05 §5.4/5.6）：
    /// 好客值 = 200 基础 + 平均品质×50 + 连续招待×10 + 回访+100(3天) + 当日在线+150 + 好友+50；
    /// 衰减：连续未招待 -10/天、空盆>24h -50、7 日未登录 -100。
    /// 面板展示公式与衰减提示；数值服务端权威。
    /// </summary>
    public class HospitalityManager : GameplayManager<HospitalityManager>
    {
        public int HospitalityValue { get; private set; }

        /// <summary>按服务端快照刷新（GET /api/v1/user/profile 返回好客值）。</summary>
        public void RefreshFromServer(int value)
        {
            HospitalityValue = value;
            Debug.Log($"[HospitalityManager] 好客值 = {value}");
            // TODO: EventBus 广播 UI 刷新
        }

        /// <summary>展示公式/衰减提示（供面板调用，文案按文档 05）。</summary>
        public string BuildFormulaText()
        {
            return "200 基础 + 平均品质×50 + 连续招待×10 + 回访+100(3天) + 在线+150 + 好友+50";
        }
    }
}
