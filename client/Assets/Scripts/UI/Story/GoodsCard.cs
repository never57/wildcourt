using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

/// <summary>
/// goodsCard 商品卡脚本：
/// - 总价 totalNumber = 单价 unitPriceNumber × 购买数量 purchaseQuantityNumber
/// - 点 drop 按钮 -> Slider 值减 1；点 rise 按钮 -> Slider 值加 1
/// 把本脚本拖到 goodsCard 根物体上即可，引用会自动按名字查找。
/// </summary>
public class GoodsCard : MonoBehaviour
{
    [Tooltip("单价数字文本（unitPriceNumber）")]
    [SerializeField] private TMP_Text unitPriceText;

    [Tooltip("购买数量数字文本（purchaseQuantityNumber）")]
    [SerializeField] private TMP_Text quantityText;

    [Tooltip("总价数字文本（totalNumber）")]
    [SerializeField] private TMP_Text totalText;

    [Tooltip("数量滑块")]
    [SerializeField] private Slider slider;

    [Tooltip("减少按钮（drop）")]
    [SerializeField] private Button dropButton;

    [Tooltip("增加按钮（rise）")]
    [SerializeField] private Button riseButton;

    [Tooltip("商品名文本（goodsName）")]
    [SerializeField] private TMP_Text nameText;

    [Tooltip("商品图标（goodsImage）")]
    [SerializeField] private Image iconImage;

    [Tooltip("剩余可购买文本（remainingCount）")]
    [SerializeField] private TMP_Text remainingText;

    [Tooltip("已拥有文本（StockText）")]
    [SerializeField] private TMP_Text ownedText;

    [Tooltip("购买按钮（BUY）")]
    [SerializeField] private Button buyButton;

    [Tooltip("点击购买时触发，参数 = 当前购买数量")]
    public UnityEvent<int> onBuy = new UnityEvent<int>();

    private bool warnedRemainingNull;   // 只警告一次，防止刷屏
    private bool warnedOwnedNull;

    private void Awake()
    {
        // 没拖引用就按名字自动找（递归搜子物体）
        if (unitPriceText == null) unitPriceText = FindChild<TMP_Text>("unitPriceNumber");
        if (quantityText == null) quantityText = FindChild<TMP_Text>("purchaseQuantityNumber");
        if (totalText == null) totalText = FindChild<TMP_Text>("totalNumber");
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (dropButton == null) dropButton = FindChild<Button>("drop");
        if (riseButton == null) riseButton = FindChild<Button>("rise");
        if (nameText == null) nameText = FindChild<TMP_Text>("goodsName");
        if (iconImage == null) iconImage = FindChild<Image>("goodsImage");
        if (remainingText == null) remainingText = FindChild<TMP_Text>("remaining");
        if (ownedText == null) ownedText = FindChild<TMP_Text>("owned") ?? FindChild<TMP_Text>("Stock");
        if (buyButton == null) buyButton = FindChild<Button>("BUY");
    }

    private void Start()
    {
        if (slider == null)
        {
            Debug.LogWarning("GoodsCard: 找不到 Slider，请在 Inspector 里拖入引用", this);
            return;
        }

        // 滑块数值变化时刷新显示（包括手动拖动滑块）
        slider.onValueChanged.AddListener(_ => UpdateDisplay());

        // drop 减 1、rise 加 1（Slider 会自动限制在 Min/Max 范围内）
        if (dropButton != null) dropButton.onClick.AddListener(() => slider.value -= 1f);
        if (riseButton != null) riseButton.onClick.AddListener(() => slider.value += 1f);

        // BUY 点击：把当前数量抛给外部（ShopPage 负责扣钱/扣库存）
        if (buyButton != null) buyButton.onClick.AddListener(HandleBuyClick);

        UpdateDisplay();
    }

    /// <summary>给这张卡填充数据：商品名、图标、单价，并把数量归零。</summary>
    public void Setup(string goodsName, Sprite icon, float unitPrice)
    {
        if (nameText != null) nameText.text = goodsName;
        if (iconImage != null && icon != null) iconImage.sprite = icon;
        if (unitPriceText != null) unitPriceText.text = FormatNumber(unitPrice);
        if (slider != null) slider.value = slider.minValue;
        UpdateDisplay();
    }

    /// <summary>设置购买数量上限（滑块最大值）。</summary>
    public void SetMaxQuantity(int max)
    {
        if (slider == null) return;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(slider.value, slider.minValue, slider.maxValue);
        UpdateDisplay();
    }

    /// <summary>设置「剩余可购买」文本（自由格式）。</summary>
    public void SetRemainingText(string text)
    {
        if (remainingText != null) remainingText.text = text;
    }

    /// <summary>设置「剩余可购买」数量，自动显示成「剩余可购买：N」。</summary>
    public void SetRemaining(int count)
    {
        if (remainingText == null)
        {
            if (!warnedRemainingNull)
            {
                warnedRemainingNull = true;
                Debug.LogWarning("GoodsCard: 找不到「剩余可购买」文本（名字带 remaining 的 TMP 文本）", this);
            }
            return;
        }
        remainingText.text = "剩余可购买：" + count;
    }

    /// <summary>显示 / 隐藏「剩余可购买」文本（开关用）。</summary>
    public void SetRemainingVisible(bool visible)
    {
        if (remainingText != null) remainingText.gameObject.SetActive(visible);
    }

    /// <summary>设置「已拥有」文本（自由格式）。</summary>
    public void SetOwnedText(string text)
    {
        if (ownedText != null) ownedText.text = text;
    }

    /// <summary>设置「已拥有」数量，自动显示成「已拥有：N」。</summary>
    public void SetOwned(int count)
    {
        if (ownedText == null)
        {
            if (!warnedOwnedNull)
            {
                warnedOwnedNull = true;
                Debug.LogWarning("GoodsCard: 找不到「已拥有」文本（名字带 owned / Stock 的 TMP 文本）", this);
            }
            return;
        }
        ownedText.text = "已拥有：" + count;
    }

    /// <summary>当前选择的购买数量（取整）。</summary>
    public int GetQuantity()
    {
        return slider != null ? Mathf.RoundToInt(slider.value) : 0;
    }

    /// <summary>设置购买数量（购买完成后归零用）。</summary>
    public void SetQuantity(int quantity)
    {
        if (slider == null) return;
        slider.value = Mathf.Clamp(quantity, slider.minValue, slider.maxValue);
        UpdateDisplay();
    }

    /// <summary>设置数量范围：min = 滑块下限，max = 上限。</summary>
    public void SetQuantityRange(int min, int max)
    {
        if (slider == null) return;
        slider.minValue = min;
        slider.maxValue = max;
        slider.value = Mathf.Clamp(slider.value, slider.minValue, slider.maxValue);
        UpdateDisplay();
    }

    /// <summary>把数量重置到滑块下限（购买完成后用）。</summary>
    public void ResetQuantity()
    {
        if (slider == null) return;
        slider.value = slider.minValue;
        UpdateDisplay();
    }

    /// <summary>BUY 按钮点击：数量 > 0 才抛事件。</summary>
    private void HandleBuyClick()
    {
        if (slider == null || slider.value <= 0f) return;
        onBuy.Invoke(Mathf.RoundToInt(slider.value));
    }

    /// <summary>刷新显示：数量 = Slider 值，总价 = 单价 × 数量</summary>
    private void UpdateDisplay()
    {
        int quantity = Mathf.RoundToInt(slider.value);
        float unitPrice = ParseNumber(unitPriceText);

        float total = unitPrice * quantity;

        if (quantityText != null) quantityText.text = quantity.ToString();
        if (totalText != null) totalText.text = FormatNumber(total);
        if (buyButton != null) buyButton.interactable = quantity > 0;
    }

    /// <summary>从文本里取出数字（忽略货币符号、中文等非数字字符）</summary>
    private static float ParseNumber(TMP_Text text)
    {
        if (text == null) return 0f;
        string digits = Regex.Replace(text.text, "[^0-9.]", "");
        return float.TryParse(digits, out float value) ? value : 0f;
    }

    /// <summary>整数不带小数，非整数最多保留两位</summary>
    private static string FormatNumber(float value)
    {
        return value % 1f == 0f ? value.ToString("0") : value.ToString("0.##");
    }

    /// <summary>在子物体里按名字的一部分查找组件</summary>
    private T FindChild<T>(string namePart) where T : Component
    {
        foreach (T comp in GetComponentsInChildren<T>(true))
        {
            if (comp.gameObject.name.Contains(namePart))
                return comp;
        }
        return null;
    }
}
