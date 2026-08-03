using UnityEngine;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 庭院建造（文档 03 §3.2 Yard/、03）：等轴测 45° 摆放、编辑模式、3 个布局槽、
    /// 互访庭院渲染。等级容量受玩家等级约束（Sheet16 YardUnlock：农田 6→12、加工槽 2→5、
    /// 食物盆 10→20、装饰 50→200、展示柜 3→9、信箱 20→50）。
    /// </summary>
    public class YardManager : GameplayManager<YardManager>
    {
        public const int LayoutSlotCount = 3; // 3 个布局槽（文档 03 §3.2）

        // TODO(文档 03)：等轴测摆放/编辑模式、布局槽切换（PUT /api/v1/yard/layout）、等级容量校验（读 Config_YardUnlock）、互访庭院渲染（GET /api/v1/yard/visit/{userId}）

        /// <summary>保存当前布局（服务端校验容量）。</summary>
        public void SaveLayout(string layoutJson, int slotIndex)
        {
            Debug.Log($"[YardManager] 保存布局槽 {slotIndex}");
            // NetManager.Instance.Post<object>(ApiPaths.YardLayout,
            //     new LayoutSaveRequest { layoutJson = layoutJson, slotIndex = slotIndex },
            //     resp => { /* 本地快照更新 */ });
        }
    }
}
