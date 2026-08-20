using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

namespace WildCourt.UI.Farm
{
    /// <summary>
    /// 种子选择面板（Farm.prefab/SeedCanvas 组件）。
    /// 打开时拉取背包（GET /api/v1/user/inventory）过滤种子：只显示持有数 &gt; 0 的种子卡，
    /// 一张都没有则显示 noSeed 提示并收起列表。点击卡片种植（服务端 /yard/plant 校验并原子消耗
    /// 种子，本地先行扣减仅为即时反馈）；点击面板外区域（全屏遮罩）关闭，同时恢复被暂时禁用的其他 Canvas。
    /// </summary>
    public class FarmSeedPanel : MonoBehaviour
    {
        [Header("绑定")]
        [Tooltip("Farm 根节点（排除自身画布与进度条画布）")]
        [SerializeField] private Transform farmRoot;
        [SerializeField] private Transform seedContent;      // ScrollView/Viewport/Content
        [SerializeField] private GameObject seedItemPrefab;  // seedItem 预制体
        [SerializeField] private GameObject noSeed;          // 无种子提示
        [SerializeField] private GameObject scrollView;      // 种子列表容器
        [SerializeField] private FarmPlot farmPlot;          // 种植回调

        private readonly Dictionary<string, int> _owned = new();
        private readonly Dictionary<string, int> _growTimeMin = new();
        private readonly List<Canvas> _suspended = new();
        private Button _blocker;

        [Serializable]
        private sealed class SeedRow
        {
            public string id;
            public string itemName;
            public string category;
            public int buyPrice;
            public string cropId;
        }

        [Serializable]
        private sealed class CropRow
        {
            public string id;
            public string name;
            public int growTime;   // 分钟
            public int yield;
        }

        private void Awake()
        {
            LoadCrops();
        }

        /// <summary>作物生长时长表（Config_Crops.json，growTime 分钟）。</summary>
        private void LoadCrops()
        {
            string json = ConfigMgr.Instance != null ? ConfigMgr.Instance.GetRawJson("Crops") : null;
            if (string.IsNullOrEmpty(json)) return;
            foreach (var row in JsonUtil.FromJsonArray<CropRow>(json))
            {
                if (row != null && row.growTime > 0) _growTimeMin[row.id] = row.growTime;
            }
        }

        public void Open()
        {
            gameObject.SetActive(true);
            Refresh();
            SuspendOtherCanvases();
            CreateBlocker();
        }

        public void Close()
        {
            gameObject.SetActive(false);
            ResumeCanvases();
            DestroyBlocker();
        }

        // ---------- 数据 ----------

        private void Refresh()
        {
            var net = NetManager.Instance;
            if (net == null)
            {
                BuildSeedList();
                return;
            }
            net.Get<InventoryResponse>(ApiPaths.UserInventory,
                resp =>
                {
                    _owned.Clear();
                    if (resp != null && resp.data != null && resp.data.items != null)
                    {
                        for (int i = 0; i < resp.data.items.Length; i++)
                        {
                            var item = resp.data.items[i];
                            if (item != null && item.count > 0) _owned[item.itemId] = item.count;
                        }
                    }
                    BuildSeedList();
                },
                (code, msg) =>
                {
                    Debug.LogWarning($"[FarmSeedPanel] 背包加载失败 {code}:{msg}");
                    BuildSeedList();
                });
        }

        private void BuildSeedList()
        {
            if (seedContent == null) return;
            for (int i = seedContent.childCount - 1; i >= 0; i--)
            {
                Destroy(seedContent.GetChild(i).gameObject);
            }

            string json = ConfigMgr.Instance != null ? ConfigMgr.Instance.GetRawJson("Shop") : null;
            bool any = false;
            if (!string.IsNullOrEmpty(json))
            {
                foreach (var row in JsonUtil.FromJsonArray<SeedRow>(json))
                {
                    if (row == null || !string.Equals(row.category, "seed", StringComparison.OrdinalIgnoreCase)) continue;
                    int count = _owned.TryGetValue(row.id, out var c) ? c : 0;
                    if (count <= 0 || seedItemPrefab == null || seedContent == null) continue;

                    var go = Instantiate(seedItemPrefab, seedContent);
                    var item = go.GetComponent<FarmSeedItem>();
                    if (item == null)
                    {
                        Destroy(go);
                        continue;
                    }
                    any = true;
                    var rowRef = row;
                    item.Bind(row.itemName, count, () => OnSeedClicked(rowRef));
                }
            }

            if (noSeed != null) noSeed.SetActive(!any);
            if (scrollView != null) scrollView.SetActive(any);
        }

        private void OnSeedClicked(SeedRow row)
        {
            if (farmPlot == null) return;
            int count = _owned.TryGetValue(row.id, out var c) ? c : 0;
            if (count <= 0) return;
            // 本地先行扣减做即时反馈；服务端在 /yard/plant 校验并原子消耗种子（以服务端为准）
            if (count == 1) _owned.Remove(row.id);
            else _owned[row.id] = count - 1;

            int growMin = _growTimeMin.TryGetValue(row.cropId, out var m) ? m : 1;
            farmPlot.Plant(row.cropId, growMin);
            Close();
        }

        // ---------- 其他 Canvas 挂起 / 恢复 ----------

        private void SuspendOtherCanvases()
        {
            _suspended.Clear();
            if (farmRoot == null) return;
            foreach (var canvas in FindObjectsByType<Canvas>(FindObjectsInactive.Exclude, FindObjectsSortMode.None))
            {
                if (!canvas.enabled || canvas.transform.IsChildOf(farmRoot)) continue;
                _suspended.Add(canvas);
                canvas.enabled = false;
            }
        }

        private void ResumeCanvases()
        {
            for (int i = 0; i < _suspended.Count; i++)
            {
                if (_suspended[i] != null) _suspended[i].enabled = true;
            }
            _suspended.Clear();
        }

        // ---------- 全屏遮罩（点击面板外关闭） ----------

        private void CreateBlocker()
        {
            if (_blocker != null) return;
            var go = new GameObject("ClickBlocker", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.color = new Color(0f, 0f, 0f, 0.35f);
            var btn = go.AddComponent<Button>();
            btn.transition = Selectable.Transition.None;
            btn.onClick.AddListener(Close);
            go.transform.SetAsFirstSibling();
            _blocker = btn;
        }

        private void DestroyBlocker()
        {
            if (_blocker == null) return;
            Destroy(_blocker.gameObject);
            _blocker = null;
        }
    }
}
