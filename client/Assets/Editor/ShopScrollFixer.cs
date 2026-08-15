#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 一键修复商店滚动视图：
/// 1. 把「视口 Viewport」设成四角拉伸满格；
/// 2. 把「内容 Content」设成左右拉满 + 顶部对齐；
/// 3. 给 Content 补上「内容大小适配器 Content Size Fitter」（关键：让高度自动撑开）；
/// 4. 视口补上遮罩；5. 关闭横向滚动。
///
/// 用法：顶部菜单 → 工具 → 修复商店滚动视图
/// </summary>
public static class ShopScrollFixer
{
    [MenuItem("工具/修复商店滚动视图")]
    public static void Fix()
    {
        ScrollRect sr = Object.FindObjectOfType<ScrollRect>(true);
        if (sr == null)
        {
            Debug.LogWarning("ShopScrollFixer: 场景里找不到 ScrollRect");
            return;
        }

        RectTransform viewport = sr.viewport;
        RectTransform content = sr.content;

        if (viewport == null)
        {
            Debug.LogError("ShopScrollFixer: ScrollRect 的「视口 Viewport」引用是空的，请先在 Inspector 里拖入");
            return;
        }
        if (content == null)
        {
            Debug.LogError("ShopScrollFixer: ScrollRect 的「内容 Content」引用是空的，请先在 Inspector 里拖入");
            return;
        }

        // 1) 视口：四角拉伸满格
        viewport.anchorMin = Vector2.zero;
        viewport.anchorMax = Vector2.one;
        viewport.pivot = new Vector2(0.5f, 0.5f);
        viewport.offsetMin = Vector2.zero;
        viewport.offsetMax = Vector2.zero;

        // 2) 内容：左右拉满 + 顶部对齐
        float height = content.sizeDelta.y;
        content.anchorMin = new Vector2(0f, 1f);
        content.anchorMax = new Vector2(1f, 1f);
        content.pivot = new Vector2(0.5f, 1f);
        content.sizeDelta = new Vector2(0f, height);
        content.anchoredPosition = Vector2.zero;

        // 3) 关键！给内容加「内容大小适配器」，让高度随卡片数量自动撑开
        ContentSizeFitter fitter = content.GetComponent<ContentSizeFitter>();
        if (fitter == null)
            fitter = content.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.Unconstrained;  // 宽度仍保持左右拉满
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;    // 高度自动撑开

        // 4) 视口补遮罩
        if (viewport.GetComponent<RectMask2D>() == null)
            viewport.gameObject.AddComponent<RectMask2D>();

        // 5) 只上下滚
        sr.horizontal = false;
        sr.vertical = true;

        EditorUtility.SetDirty(sr.gameObject);
        Debug.Log("ShopScrollFixer: 已修复（已给 Content 加 Content Size Fitter，垂直 = Preferred Size）。点 Play 试试。");
    }
}
#endif
