using UnityEngine;

namespace Game.Data
{
    /// <summary>
    /// UI 尺寸与字号集中配置（Step11 数据驱动，禁止散落硬编码）。
    /// 全部尺寸/字号写入 DT_UIStyle 资产，由 Applier（Editor 落盘）与控制器（Awake 注入）统一读取。
    /// 字段默认值即 Step11 目标规格，运行期无资产时同样可用。
    /// </summary>
    [CreateAssetMenu(menuName = "Game/Data/UIStyleConfig")]
    public class UIStyleConfig : ScriptableObject
    {
        [Header("字号 FontSize")]
        public int titleSize = 52;          // Txt_Title（面板标题）
        public int bodySize = 36;           // Txt / Txt_Name / Txt_Result（正文/行名/结果）
        public int descSize = 28;           // Txt_Desc/Sub/Tip/Qty/Total/UnitPrice/Sell/Primary/Empty/Count
        public int topCountSize = 46;       // 顶栏数量（Txt_Currency / Txt_Bait）
        public int rightLabelSize = 34;     // 右栏标签（Txt_Label）
        public int bubbleSize = 40;         // 气泡（Txt_Bubble）
        public int toastSize = 42;          // Toast（PF_Toast 内 Txt）
        public int mainButtonTextSize = 48; // 主按钮主文案（Txt_Main）
        public int sceneNameSize = 40;      // 场景名（Txt_SceneName）
        public int timeOfDaySize = 32;      // 时段（Txt_TimeOfDay）
        public int tensionStateSize = 34;   // 拉力状态（Txt_TensionState）
        public int catchProgressSize = 28;  // 收鱼进度（Txt_CatchProgress）

        [Header("右栏 RightBar")]
        public Vector2 rightItemSize = new Vector2(200f, 200f);   // 条目（Item_*）LayoutElement 尺寸
        public float rightIconSize = 96f;                          // 条目图标（Item_*/Img_Icon）
        public float rightSpacing = 24f;                           // VLayout 条目间距
        public float rightInset = 24f;                             // 右边缘距（根 anchoredPosition.x）

        [Header("钓鱼主按钮")]
        public float mainButtonSize = 280f;                        // Btn_MainAction
        public Vector2 mainButtonOffset = new Vector2(0f, 80f);    // anchor(0.5,0) pivot(0.5,0)：底中、上80
        public float reelButtonSize = 260f;                        // Btn_Reel

        [Header("面板 Panel")]
        public Vector2 panelBodySize = new Vector2(720f, 1300f);   // Panel_Body / PF_BagPanel / PF_BaitShopPanel（约屏宽66%、屏高67%）
        public float closeButtonSize = 64f;                        // Btn_Close（不小于40）
        public float closeButtonTextSize = 40f;                    // 关闭按钮“×”字号
        public Vector2 closeButtonOffset = new Vector2(24f, 24f);  // 左上角内边距（x右 / y上）
        public Color closeButtonBgColor = new Color(0.15f, 0.12f, 0.08f, 0.95f);   // 关闭按钮高对比底色
        public Color closeButtonMarkColor = new Color(1f, 1f, 1f, 1f);              // 关闭按钮“×”颜色

        [Header("商店 Shop")]
        public Vector2 buyButtonSize = new Vector2(200f, 84f);     // Btn_Buy
        public Vector2 qtyButtonSize = new Vector2(72f, 56f);      // 数量钮（x1/x5/x10/Minus/Plus/Txt_Qty）
        public float shopRowHeight = 190f;                         // PF_ShopItemRow 行高

        [Header("背包 Bag")]
        public float bagRowHeight = 170f;                          // PF_InventoryItemRow 行高
        public float bagIconSize = 100f;                           // 背包行图标
        public float bagFrameSize = 114f;                          // 背包行边框（Img_Frame）

        [Header("顶栏 TopBar")]
        public float topBarIconSize = 72f;                         // 顶栏图标（Group_*/Img_Icon）
        public float topBarGroupHeight = 96f;                      // 胶囊高度（Group_Currency / Group_Bait）
        public float topBarPlusSize = 96f;                         // Plus 按钮（Btn_CurrencyPlus / Btn_BaitPlus）

        [Header("拉力面板 Tension")]
        public float tensionSliderHeight = 44f;                    // Slider_Tension 高度
        public float catchSliderHeight = 24f;                      // Slider_CatchProgress 高度
    }
}
