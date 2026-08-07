using UnityEngine;

namespace WildCourt.Gameplay.Nonfiction
{
    /// <summary>
    /// 非遗制作（文档 03 §3.2 Nonfiction/、06）：34 省非遗 NF01~NF34，
    /// 9 种 miniGameType（cut/carve/embroider/weave/paint/shape/brush/assemble/mix），
    /// 步骤表现与模板配置（Sheet7 Nonfictions 的 template JSON）。
    /// </summary>
    public class NonfictionManager : GameplayManager<NonfictionManager>
    {
        /// <summary>互动玩法类型（文档 06 §6.4.2）。</summary>
        public enum MiniGameType { Cut, Carve, Embroider, Weave, Paint, Shape, Brush, Assemble, Mix }

        // TODO(文档 06)：34 省非遗列表（Config_Nonfictions）、材料校验（加工产物/商店专属材料 3 种）、步骤表现、展示柜约束（仅他赠可入柜）

        /// <summary>制作完成结算（由各 miniGame 子逻辑调用）。</summary>
        public void CompleteCraft(string nonfictionId)
        {
            Debug.Log($"[NonfictionManager] 完成制作 {nonfictionId}");
            // TODO: 入背包；展示柜货源仅限拜访留下/回赠/回访所得（文档 05 §5.4）
        }
    }
}
