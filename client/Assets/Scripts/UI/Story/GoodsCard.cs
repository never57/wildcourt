using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
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

    private void Awake()
    {
        // 没拖引用就按名字自动找（递归搜子物体）
        if (unitPriceText == null) unitPriceText = FindChild<TMP_Text>("unitPriceNumber");
        if (quantityText == null) quantityText = FindChild<TMP_Text>("purchaseQuantityNumber");
        if (totalText == null) totalText = FindChild<TMP_Text>("totalNumber");
        if (slider == null) slider = GetComponentInChildren<Slider>(true);
        if (dropButton == null) dropButton = FindChild<Button>("drop");
        if (riseButton == null) riseButton = FindChild<Button>("rise");
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

        UpdateDisplay();
    }

    /// <summary>刷新显示：数量 = Slider 值，总价 = 单价 × 数量</summary>
    private void UpdateDisplay()
    {
        int quantity = Mathf.RoundToInt(slider.value);
        float unitPrice = ParseNumber(unitPriceText);

        float total = unitPrice * quantity;

        if (quantityText != null) quantityText.text = quantity.ToString();
        if (totalText != null) totalText.text = FormatNumber(total);
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
