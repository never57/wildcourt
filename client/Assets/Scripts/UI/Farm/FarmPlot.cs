using System;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.Farm
{
    /// <summary>
    /// 农田控制器（Farm.prefab 根节点组件）。
    /// 空地点击 → 弹种子面板（SeedCanvas）→ 选种种植（消耗 1 颗种子）→ 生长（期间不可点击，
    /// Plant 按 25/50/75/100 切换阶段，进度条填充与剩余时间同步）→ 成熟点击植株收获。
    /// 数据模型复用 Gameplay/Crop 的 CropPlot；Plant 为占位节点，后续可直接替换为
    /// 真实植株预制体（子节点保持 25/50/75/100 命名，或实现 IPlantView）。
    /// 服务端模式（useServer）：种植上报 POST /api/v1/yard/plant（服务端校验并原子消耗种子）、
    /// 收获 POST /api/v1/yard/harvest（产量入背包）、启动时 GET /api/v1/yard/plots 恢复种植状态；
    /// 关闭 useServer 则纯本地模拟（growTimeScale 加速生效）。
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
        [Tooltip("生长加速倍率（1 = 配置表 growTime 分钟；服务端模式下不生效，时长与 readyAt 对齐）")]
        [SerializeField, Range(0.1f, 60f)] private float growTimeScale = 1f;

        [Header("服务端")]
        [Tooltip("走服务端结算（种植/收获/状态恢复走 /yard/* 接口）；关闭后纯本地模拟，便于无网络调试")]
        [SerializeField] private bool useServer = true;
        [Tooltip("本农田的服务端格子号（0 起，上限见 Config_YardUnlock.farmlandCount）")]
        [SerializeField] private int cellId = 0;

        private readonly CropPlot _plot = new CropPlot(0);
        private bool _restored;

        private void Start()
        {
            if (seedPanel == null) seedPanel = GetComponentInChildren<FarmSeedPanel>(true);
            if (plantView == null && plantRoot != null) plantView = plantRoot.GetComponentInChildren<PlantStageView>(true);
            if (useServer) RestoreFromServer();
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

        /// <summary>
        /// 种植（growTimeMin 为配置表分钟数）：服务端模式下上报 /yard/plant（服务端校验并消耗种子），
        /// 本地按真实时长乐观起种，上报失败回滚；纯本地模式按 growTimeScale 加速。
        /// </summary>
        public void Plant(string cropId, float growTimeMin)
        {
            if (useServer)
            {
                PlantOnServer(cropId, growTimeMin);
                return;
            }
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
            if (useServer)
            {
                HarvestOnServer();
                return;
            }
            _plot.Harvest();
            RefreshView();
            Debug.Log("[FarmPlot] 收获完成（本地模拟）");
        }

        // ---------- 服务端结算（/api/v1/yard/*） ----------

        private void PlantOnServer(string cropId, float growTimeMin)
        {
            long readyAt = UnixNow() + (long)(growTimeMin * 60f);
            // 本地乐观起种（真实时长，与 readyAt 对齐；growTimeScale 仅本地模拟用）
            _plot.PlantSeconds(cropId, readyAt - UnixNow());
            RefreshView();

            var net = NetManager.Instance;
            if (net == null) return;
            net.Post<FarmPlotDto>(ApiPaths.YardPlant,
                new PlantCropRequest { cellId = cellId, cropId = cropId, readyAt = readyAt },
                resp => Debug.Log($"[FarmPlot] 种植上报成功: {cropId} 格 {cellId} readyAt {readyAt}"),
                (code, msg) =>
                {
                    Debug.LogWarning($"[FarmPlot] 种植失败 {code}:{msg}，回滚本地状态");
                    // 服务端拒绝（种子不足/格子占用/未登录等）：回滚乐观起种
                    if (_plot.State == CropPlot.PlotState.Growing && _plot.CropId == cropId)
                    {
                        _plot.Harvest();
                        RefreshView();
                    }
                });
        }

        private void HarvestOnServer()
        {
            var net = NetManager.Instance;
            if (net == null) return;
            string cropId = _plot.CropId;
            net.Post<HarvestResultDto>(ApiPaths.YardHarvest, new HarvestCropRequest { cellId = cellId },
                resp =>
                {
                    _plot.Harvest();
                    RefreshView();
                    int count = resp != null && resp.data != null ? resp.data.count : 0;
                    Debug.Log($"[FarmPlot] 收获成功: {cropId} x{count}（已入背包）");
                },
                (code, msg) => Debug.LogWarning($"[FarmPlot] 收获失败 {code}:{msg}（作物尚未成熟或状态不同步，可稍后重试）"));
        }

        /// <summary>启动恢复：拉取服务端种植状态，按剩余时间重建倒计时。</summary>
        private void RestoreFromServer()
        {
            if (_restored) return;
            _restored = true;
            var net = NetManager.Instance;
            if (net == null || string.IsNullOrEmpty(net.Token))
            {
                Debug.LogWarning("[FarmPlot] 未登录，跳过种植状态恢复");
                return;
            }
            net.Get<FarmPlotsResponse>(ApiPaths.YardPlots,
                resp =>
                {
                    if (resp == null || resp.data == null || resp.data.plots == null) return;
                    foreach (var p in resp.data.plots)
                    {
                        if (p == null || p.cellId != cellId) continue;
                        long remain = p.readyAt - UnixNow();
                        if (remain > 0)
                        {
                            _plot.PlantSeconds(p.cropId, remain);   // 按剩余秒数重建生长（成熟时刻与 readyAt 对齐）
                        }
                        else
                        {
                            _plot.PlantSeconds(p.cropId, 1f);       // 已过 readyAt：直接置为成熟可收获
                            _plot.ForceMature();
                        }
                        RefreshView();
                        Debug.Log($"[FarmPlot] 恢复种植状态: {p.cropId} 格 {cellId} 剩余 {Mathf.Max(0f, remain)}s");
                        return;
                    }
                },
                (code, msg) => Debug.LogWarning($"[FarmPlot] 种植状态恢复失败 {code}:{msg}"));
        }

        private static long UnixNow()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
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
