using UnityEngine;

namespace WildCourt.Gameplay.Pet
{
    /// <summary>
    /// 宠物（文档 03 §3.2 Pet/、07）：宠物面板、状态机表现（在家→派遣中→休息中→在家）、
    /// 派遣动画、归家日报。宠物等级 1~20，访问数 = floor((等级+2)/5)+2（上限 5，文档 05 §5.5）。
    /// </summary>
    public class PetManager : GameplayManager<PetManager>
    {
        public enum PetStatus { AtHome, Dispatch, Resting }

        // TODO(文档 07)：宠物属性（type/level/exp/status）、派遣 10 步校验 UI、归家日报弹窗、经验（派遣归来+10/留信+5）

        /// <summary>派遣入口（POST /api/v1/pet/dispatch，十步校验服务端执行）。</summary>
        public void Dispatch(int provinceId, int cityId, string nonfictionId, string letterContent)
        {
            Debug.Log($"[PetManager] 派遣 → 省 {provinceId} 市 {cityId} 随行非遗 {nonfictionId}");
            // NetManager.Instance.Post<DispatchResultResponse>(ApiPaths.PetDispatch,
            //     new DispatchRequest { provinceId = provinceId, cityId = cityId, nonfictionId = nonfictionId, letterContent = letterContent },
            //     resp => { /* 匹配结果展示 → MatchManager */ });
        }

        /// <summary>访问数计算（服务端同公式）。</summary>
        public static int CalcVisitCount(int level)
        {
            return Mathf.Min(5, (level + 2) / 5 + 2);
        }
    }
}
