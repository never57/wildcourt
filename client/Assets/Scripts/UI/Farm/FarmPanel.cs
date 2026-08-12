using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Gameplay.Yard;
using WildCourt.Lua;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.UI.Farm
{
    /// <summary>
    /// 农场面板（预制体驱动）：UI 结构全部由 FarmPanel.prefab 在编辑器中搭建
    /// （本类不做任何代码建 UI），代码只负责实例化预制体、绑定字段与数据刷新。
    /// 玩法逻辑由 Lua FarmManager 持有（Plant/Harvest/SellCrop），结果经 WC.event
    /// 的 Farm.* 事件回传刷新。
    ///
    /// 预制体搭建要求（Unity 会话按此创建 FarmPanel.prefab）：
    ///   根节点挂 FarmPanel；序列化字段：shellText/hintText/gridRoot/cellPrefab/
    ///   seedListRoot/seedItemPrefab/panelRoot；
    ///   cellPrefab 挂 FarmCell（nameText/timeText/button）；
    ///   seedItemPrefab 挂 FarmSeedItem（nameText/priceText/button）。
    /// </summary>
    public class FarmPanel : MonoBehaviour
    {
        public static FarmPanel Instance { get; private set; }

        /// <summary>跨场景注入的 FarmPanel 预制体（登录时注入，支持热更）。</summary>
        public static GameObject FarmPanelPrefab { get; set; }

        [Header("预制体绑定（FarmPanel.prefab，编辑器搭建）")]
        [SerializeField] private Text shellText;
        [SerializeField] private Text hintText;
        [SerializeField] private Transform gridRoot;
        [SerializeField] private GameObject cellPrefab;
        [SerializeField] private Transform seedListRoot;
        [SerializeField] private GameObject seedItemPrefab;
        [SerializeField] private GameObject panelRoot;

        private readonly Dictionary<int, FarmCell> _cells = new();
        private readonly Dictionary<int, PlotView> _plots = new();
        private int _selectedCell = -1;
        private int _shell;
        private float _refreshTimer = 1f;
        private bool _subscribed;

        /// <summary>本地展示用的地块状态（数据来源 Lua FarmManager.GetPlots）。</summary>
        private sealed class PlotView
        {
            public string CropId;
            public string CropName;
            public string State;
            public long ReadyAt;
        }

        /// <summary>商店配置行（Config_Shop.json 种子项）。</summary>
        [System.Serializable]
        private sealed class SeedRow
        {
            public string id;
            public string itemName;
            public string category;
            public int buyPrice;
            public string cropId;
        }

        public static FarmPanel CreateOrInstantiate()
        {
            if (FarmPanelPrefab == null)
            {
                Debug.LogWarning("[FarmPanel] 未注入 FarmPanel 预制体（登录时注入 farmPanelPrefab），跳过打开");
                return null;
            }
            if (Instance != null)
            {
                Instance.Open();
                return Instance;
            }
            var go = Instantiate(FarmPanelPrefab);
            var panel = go.GetComponent<FarmPanel>();
            if (panel == null)
            {
                Destroy(go);
                Debug.LogWarning("[FarmPanel] 预制体缺少 FarmPanel 组件");
                return null;
            }
            return panel;
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }

        private void Start()
        {
            SubscribeEvents();
            BuildCells();
            BuildSeedList();
            RefreshShell();
            RefreshPlots();
            SetHint("点击空地选种子种植；成熟后点击收获");
        }

        private void Update()
        {
            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                _refreshTimer = 1f;
                RefreshPlots();
            }
        }

        // ---------- 事件订阅（结果经 WC.event 回传） ----------

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
#if USE_XLUA
            LuaEventBridge.Subscribe("Farm.PlantResult", OnPlantResult);
            LuaEventBridge.Subscribe("Farm.HarvestResult", OnHarvestResult);
            LuaEventBridge.Subscribe("Farm.SellResult", OnSellResult);
            LuaEventBridge.Subscribe("Farm.Mature", OnMature);
#endif
        }

#if USE_XLUA
        private void OnPlantResult(LuaTable payload)
        {
            bool ok = Bool(payload.Get<object>("ok"));
            SetHint(ok ? "种植成功" : "种植失败: " + Str(payload.Get<object>("msg")));
            RefreshPlots();
        }

        private void OnHarvestResult(LuaTable payload)
        {
            bool ok = Bool(payload.Get<object>("ok"));
            SetHint(ok
                ? "收获成功: 小麦 x" + Long(payload.Get<object>("count"))
                : "收获失败: " + Str(payload.Get<object>("msg")));
            RefreshPlots();
        }

        private void OnSellResult(LuaTable payload)
        {
            bool ok = Bool(payload.Get<object>("ok"));
            SetHint(ok ? "出售成功" : "出售失败: " + Str(payload.Get<object>("msg")));
            long shell = Long(payload.Get<object>("shell"));
            if (shell > 0 && shellText != null) shellText.text = "庭贝 " + shell;
        }

        private void OnMature(LuaTable payload)
        {
            int cellId = Int(payload.Get<object>("cellId"));
            SetHint("作物成熟啦，点击收获！");
            RefreshPlots();
        }
#endif

        // ---------- 预制体实例化 ----------

        private void BuildCells()
        {
            if (gridRoot == null || cellPrefab == null) return;
            int count = YardManager.Instance != null && YardManager.Instance.Cap != null
                ? Mathf.Max(6, YardManager.Instance.Cap.farmlandCount)
                : 6;
            for (int i = 0; i < count; i++)
            {
                var go = Instantiate(cellPrefab, gridRoot);
                var cell = go.GetComponent<FarmCell>();
                if (cell == null)
                {
                    Destroy(go);
                    continue;
                }
                cell.Bind(i, OnCellClicked);
                _cells[i] = cell;
            }
        }

        private void BuildSeedList()
        {
            if (seedListRoot == null || seedItemPrefab == null) return;
            string json = ConfigMgr.Instance.GetRawJson("Shop");
            if (string.IsNullOrEmpty(json)) return;
            foreach (var row in JsonUtil.FromJsonArray<SeedRow>(json))
            {
                if (!string.Equals(row.category, "seed", System.StringComparison.OrdinalIgnoreCase)) continue;
                var go = Instantiate(seedItemPrefab, seedListRoot);
                var item = go.GetComponent<FarmSeedItem>();
                if (item == null)
                {
                    Destroy(go);
                    continue;
                }
                string cropId = row.cropId;
                item.Bind(row.itemName, row.buyPrice, () => PlantSeed(cropId));
            }
        }

        // ---------- 交互 ----------

        private void OnCellClicked(int cellId)
        {
            _plots.TryGetValue(cellId, out var plot);
            if (plot == null || plot.State == "empty")
            {
                _selectedCell = cellId;
                SetHint("选择种子种植到格 " + cellId);
                return;
            }
            if (plot.State == "mature")
            {
                FarmHarvest(cellId);
                return;
            }
            SetHint("格 " + cellId + " 生长中（" + plot.CropName + "）");
        }

        private void PlantSeed(string cropId)
        {
            if (_selectedCell < 0) return;
#if USE_XLUA
            if (LuaManager.Instance != null)
            {
                LuaManager.Instance.CallFunc<object>("Logic.Farm.FarmManager", "Plant", _selectedCell, cropId);
            }
#endif
        }

        private void FarmHarvest(int cellId)
        {
#if USE_XLUA
            if (LuaManager.Instance != null)
            {
                LuaManager.Instance.CallFunc<object>("Logic.Farm.FarmManager", "Harvest", cellId);
            }
#endif
        }

        // ---------- 刷新 ----------

        private void RefreshPlots()
        {
#if USE_XLUA
            if (LuaManager.Instance == null) return;
            var table = LuaManager.Instance.CallFunc<LuaTable>("Logic.Farm.FarmManager", "GetPlots");
            if (table == null) return;
            _plots.Clear();
            table.ForEach<object, object>((k, v) =>
            {
                if (v is LuaTable row)
                {
                    int cellId = Int(row.Get<object>("cellId"));
                    _plots[cellId] = new PlotView
                    {
                        CropId = Str(row.Get<object>("cropId")),
                        CropName = Str(row.Get<object>("cropName")),
                        State = Str(row.Get<object>("state")),
                        ReadyAt = Long(row.Get<object>("readyAt")),
                    };
                }
            });
            foreach (var kv in _cells) UpdateCell(kv.Key, kv.Value);
#endif
        }

        private void UpdateCell(int cellId, FarmCell cell)
        {
            _plots.TryGetValue(cellId, out var plot);
            if (plot == null)
            {
                cell.SetView("空地", "", new Color(0.55f, 0.75f, 0.45f));
                return;
            }
            if (plot.State == "mature")
            {
                cell.SetView(plot.CropName, "可收获", new Color(1f, 0.85f, 0.35f));
                return;
            }
            long remain = plot.ReadyAt - TimeUtil.NowUnixSeconds();
            cell.SetView(plot.CropName, remain > 0 ? FormatTime(remain) : "成熟中", new Color(0.7f, 0.85f, 0.55f));
        }

        private void RefreshShell()
        {
            if (NetManager.Instance == null) return;
            NetManager.Instance.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp =>
                {
                    _shell = resp.data != null ? (int)resp.data.shell : 0;
                    if (shellText != null) shellText.text = "庭贝 " + _shell;
                },
                (code, msg) => SetHint("庭贝加载失败: " + msg));
        }

        private void SetHint(string msg)
        {
            if (hintText != null) hintText.text = msg;
        }

        private static string FormatTime(long sec)
        {
            long m = sec / 60;
            long s = sec % 60;
            var sb = new System.Text.StringBuilder(8);
            sb.Append(m);
            sb.Append(':');
            if (s < 10) sb.Append('0');
            sb.Append(s);
            return sb.ToString();
        }

        // ---------- Lua 值转换 ----------

#if USE_XLUA
        private static string Str(object v) => v == null ? string.Empty : v.ToString();
        private static long Long(object v) => v == null ? 0L : System.Convert.ToInt64(v);
        private static int Int(object v) => v == null ? 0 : System.Convert.ToInt32(v);
        private static bool Bool(object v) => v is bool b && b;
#endif
    }

}
