using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace WildCourt.UI.Farm
{
    /// <summary>
    /// 农田控制器（Farm.prefab 根节点组件）。
    /// 空地点击 → 弹种子面板（SeedCanvas）→ 选种种植（消耗 1 颗种子）→ 生长（期间不可点击，
    /// Plant 按 25/50/75/100 切换阶段，进度条填充与剩余时间同步）→ 成熟点击植株收获。
    /// 数据模型复用 Gameplay/Crop 的 CropPlot；Plant 为占位节点，后续可直接替换为
    /// 真实植株预制体（子节点保持 25/50/75/100 命名，或实现 IPlantView）。
    /// </summary>
    public class FarmPlot : MonoBehaviour
    {
        [Header("地块")]
        [Tooltip("farm 地面（含 BoxCollider2D，点击判定用）")]
        [SerializeField] private Transform farmGround;
        [Tooltip("Plant 节点（占位，后续换真实植株预制体）")]
        [SerializeField] private Transform plantRoot;
        [Tooltip("植株阶段展示组件（25/50/75/100 子节点）")]
        [SerializeField] private PlantStageView plantView;

        [Header("进度条")]
        [SerializeField] private GameObject progressBar;        // 世界空间进度条根节点
        [SerializeField] private Image progressFill;            // 填充（Filled / Horizontal）
        [SerializeField] private TextMeshProUGUI progressText;  // 剩余时间文本

        [Header("种子面板")]
        [SerializeField] private FarmSeedPanel seedPanel;

        [Header("调试")]
        [Tooltip("生长加速倍率（1 = 配置表 growTime 分钟）")]
        [SerializeField, Range(0.1f, 60f)] private float growTimeScale = 1f;

        private readonly CropPlot _plot = new CropPlot(0);

        private void Start()
        {
            if (seedPanel == null) seedPanel = GetComponentInChildren<FarmSeedPanel>(true);
            if (plantView == null && plantRoot != null) plantView = plantRoot.GetComponentInChildren<PlantStageView>(true);
            RefreshView();
        }

        private void Update()
        {
            if (_plot.State == CropPlot.PlotState.Growing)
            {
                _plot.Tick();
                if (_plot.State == CropPlot.PlotState.Mature) RefreshView(); // 成熟：切 100% 植株并收起进度条
                else RefreshProgress();
            }

            if (Input.GetMouseButtonDown(0) && EventSystem.current != null && !EventSystem.current.IsPointerOverGameObject())
            {
                TryClickFarm();
            }
        }

        /// <summary>种植：种子消耗由面板完成，此处开始生长（growTimeMin 为配置表分钟数）。</summary>
        public void Plant(string cropId, float growTimeMin)
        {
            _plot.PlantSeconds(cropId, growTimeMin * 60f / Mathf.Max(0.1f, growTimeScale));
            RefreshView();
        }

        private void TryClickFarm()
        {
            if (_plot.State == CropPlot.PlotState.Growing) return; // 生长中不可点击
            if (farmGround == null) return;
            var cam = Camera.main;
            if (cam == null) return;
            var world = cam.ScreenToWorldPoint(Input.mousePosition);
            world.z = 0f;
            var hit = Physics2D.OverlapPoint(world);
            if (hit == null || !hit.transform.IsChildOf(farmGround)) return;

            if (_plot.State == CropPlot.PlotState.Empty)
            {
                if (seedPanel != null) seedPanel.Open();
            }
            else if (_plot.State == CropPlot.PlotState.Mature)
            {
                Harvest();
            }
        }

        private void Harvest()
        {
            _plot.Harvest();
            RefreshView();
            Debug.Log("[FarmPlot] 收获完成（作物入账待服务端 /api/v1/yard/harvest 接入）");
        }

        /// <summary>按地块状态刷新 Plant 可见性、生长阶段与进度条。</summary>
        private void RefreshView()
        {
            bool hasCrop = _plot.State != CropPlot.PlotState.Empty;
            if (plantRoot != null) plantRoot.gameObject.SetActive(hasCrop);
            if (progressBar != null) progressBar.SetActive(_plot.State == CropPlot.PlotState.Growing);

            if (plantView != null)
            {
                int stage = _plot.State == CropPlot.PlotState.Mature ? 3 : GrowthQuarter(_plot.Progress);
                plantView.SetStage(stage);
            }
            RefreshProgress();
        }

        private void RefreshProgress()
        {
            if (progressFill != null) progressFill.fillAmount = _plot.Progress;
            if (progressText == null || _plot.State != CropPlot.PlotState.Growing) return;
            float remain = Mathf.Max(0f, _plot.GrowDurationSec - (Time.time - _plot.PlantTime));
            int m = Mathf.FloorToInt(remain / 60f);
            int s = Mathf.CeilToInt(remain % 60f);
            progressText.text = m > 0 ? $"还剩{m}分{s}秒成熟" : $"还剩{s}秒成熟";
        }

        /// <summary>生长进度 → 植株阶段：&lt;25% 未出苗（-1），25/50/75% 分别切 0/1/2，100%（成熟）由调用方传 3。</summary>
        private static int GrowthQuarter(float progress)
        {
            if (progress < 0.25f) return -1;
            if (progress < 0.5f) return 0;
            if (progress < 0.75f) return 1;
            return 2;
        }
    }
}
