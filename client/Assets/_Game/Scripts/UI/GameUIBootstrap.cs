using Game.Data;
using Game.Services;
using UnityEngine;
using UnityEngine.EventSystems;

namespace Game.UI
{
    /// <summary>
    /// HUD 引导：初始化 InventoryService，实例化顶栏/功能栏，创建 ToastRoot 并挂 ToastManager。
    /// </summary>
    public class GameUIBootstrap : MonoBehaviour
    {
        [SerializeField] private FishingDataTable dataTable;
        [SerializeField] private UIStyleConfig style;   // DT_UIStyle（Step11 全量 UI 样式）
        [SerializeField] private GameObject topBarPrefab;
        [SerializeField] private GameObject rightBarPrefab;
        [SerializeField] private GameObject toastPrefab;
        [SerializeField] private GameObject bagPanelPrefab;
        [SerializeField] private GameObject inventoryRowPrefab;
        [SerializeField] private GameObject shopPanelPrefab;
        [SerializeField] private GameObject shopRowPrefab;

        private void Awake()
        {
            // 0. 确保场景存在 EventSystem；缺失时 UI 无法接收任何点击（商店/背包/抛竿全部失效）
            if (EventSystem.current == null)
            {
                var go = new GameObject("EventSystem");
                go.AddComponent<EventSystem>();
                go.AddComponent<StandaloneInputModule>();
            }

            // 0. 注册全局 UI 样式（DT_UIStyle），各控制器 Awake 读取注入
            if (style != null) UIStyleService.Register(style);

            // 1. 初始化背包/货币服务
            if (dataTable != null)
                InventoryService.Instance.Init(dataTable);
            else
                Debug.LogWarning("[HUD] GameUIBootstrap 未绑定 dataTable，跳过 Init");

            var canvas = GetComponentInParent<Canvas>();

            // 2. 实例化顶栏 / 功能栏
            if (topBarPrefab != null)
                Instantiate(topBarPrefab, canvas.transform);
            if (rightBarPrefab != null)
                Instantiate(rightBarPrefab, canvas.transform);

            // 3. ToastRoot + ToastManager
            var toastRoot = new GameObject("ToastRoot");
            toastRoot.transform.SetParent(canvas.transform, false);
            var mgr = toastRoot.AddComponent<ToastManager>();
            mgr.SetPrefab(toastPrefab);

            // 4. 背包面板
            if (bagPanelPrefab != null)
            {
                var bag = Instantiate(bagPanelPrefab, canvas.transform);
                var ctrl = bag.GetComponent<BagPanelController>();
                if (ctrl != null)
                    ctrl.Init(dataTable, inventoryRowPrefab);
            }

            // 5. 商店面板
            if (shopPanelPrefab != null)
            {
                var shop = Instantiate(shopPanelPrefab, canvas.transform);
                var ctrl = shop.GetComponent<ShopPanelController>();
                if (ctrl != null)
                {
                    Transform content = null;
                    var scroll = shop.transform.Find("Panel_Body/ScrollView_Items/Viewport/Content");
                    if (scroll != null) content = scroll;
                    ctrl.Init(dataTable, shopRowPrefab, content);
                }
            }
        }
    }
}
