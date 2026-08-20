using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.UI.Farm
{
    /// <summary>
    /// 植株阶段展示（Plant 节点组件，占位实现）：管理名为 25/50/75/100 的四个子节点，
    /// 按生长阶段只激活对应一个；贴图自动取 Resources/Art/Sprite/Farm/farm（PSD 同名图层）。
    /// 后续替换真实植株预制体：保持子节点命名，或自行实现 <see cref="IPlantView"/> 替换本组件。
    /// </summary>
    public class PlantStageView : MonoBehaviour, IPlantView
    {
        private static readonly string[] StageNames = { "25", "50", "75", "100" };
        private readonly List<Transform> _stages = new();
        private bool _loaded;

        /// <summary>切换生长阶段。-1 未出苗（全部隐藏），0..3 对应 25/50/75/100。</summary>
        public void SetStage(int stage)
        {
            EnsureLoaded();
            for (int i = 0; i < _stages.Count; i++)
            {
                if (_stages[i] != null) _stages[i].gameObject.SetActive(i == stage);
            }
        }

        private void EnsureLoaded()
        {
            if (_loaded) return;
            _loaded = true;
            for (int i = 0; i < StageNames.Length; i++)
            {
                var t = transform.Find(StageNames[i]);
                if (t == null) continue;
                _stages.Add(t);
                AssignStageSprite(t, StageNames[i]);
            }
        }

        /// <summary>给阶段节点补贴图（PSD 图层同名 sprite；已有贴图或没有 SpriteRenderer 的节点跳过）。</summary>
        private static void AssignStageSprite(Transform stage, string name)
        {
            var sr = stage.GetComponent<SpriteRenderer>();
            if (sr == null || sr.sprite != null) return;
            var sprites = Resources.LoadAll<Sprite>("Art/Sprite/Farm/farm");
            for (int i = 0; i < sprites.Length; i++)
            {
                if (sprites[i] != null && sprites[i].name == name)
                {
                    sr.sprite = sprites[i];
                    break;
                }
            }
        }
    }

    /// <summary>植株展示接口：未来真实植株预制体可实现此接口替换占位实现。</summary>
    public interface IPlantView
    {
        /// <summary>切换生长阶段。-1 未出苗（全部隐藏），0..3 对应 25/50/75/100。</summary>
        void SetStage(int stage);
    }
}
