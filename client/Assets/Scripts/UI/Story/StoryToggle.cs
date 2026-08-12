using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 挂在 story 按钮上：
/// 点击一次 -> 所有子面板（seedStory、BuildStory...）弹出；再点一次 -> 全部收回。
/// 弹出带一点回弹效果，收回是平滑缩小。
/// </summary>
public class StoryToggle : MonoBehaviour
{
    [Tooltip("要弹出的面板列表；不拖的话会自动收集子物体里名字带 story 的")]
    [SerializeField] private RectTransform[] panels;

    [Tooltip("动画时长（秒）")]
    [SerializeField] private float duration = 0.25f;

    [Tooltip("游戏开始时面板是否隐藏")]
    [SerializeField] private bool startHidden = true;

    private Vector3[] initialScales; // 每个面板的原始缩放
    private Coroutine anim;          // 正在播放的动画

    private void Awake()
    {
        if (panels == null || panels.Length == 0)
        {
            // 自动收集直接子物体里名字带 story 的（seedStory、BuildStory、storyMenu...）
            var found = new List<RectTransform>();
            foreach (Transform child in transform)
            {
                if (child.name.Contains("seedStory") ||
                    child.name.Contains("storyMenu") ||
                    child.name.ToLower().Contains("story"))
                {
                    found.Add(child as RectTransform);
                }
            }
            panels = found.ToArray();
        }
    }

    private void Start()
    {
        if (panels == null || panels.Length == 0)
        {
            Debug.LogWarning("StoryToggle: 没有找到任何面板，请在 Inspector 里把面板拖到 Panels 列表", this);
            return;
        }

        initialScales = new Vector3[panels.Length];
        for (int i = 0; i < panels.Length; i++)
        {
            initialScales[i] = panels[i].localScale;
            if (startHidden)
                panels[i].gameObject.SetActive(false);
        }
    }

    /// <summary>按钮点击调用的方法：有面板显示就全部收回，否则全部弹出</summary>
    public void Toggle()
    {
        if (panels == null || panels.Length == 0) return;

        // 打断正在播放的动画，避免连点出问题
        if (anim != null) StopCoroutine(anim);

        bool anyVisible = false;
        foreach (var p in panels)
        {
            if (p.gameObject.activeSelf)
            {
                anyVisible = true;
                break;
            }
        }

        anim = anyVisible ? StartCoroutine(Collapse()) : StartCoroutine(Pop());
    }

    /// <summary>弹出动画：从 0 放大到原始大小，带一点回弹</summary>
    private IEnumerator Pop()
    {
        foreach (var p in panels)
        {
            p.gameObject.SetActive(true);
            CanvasGroup cg = p.GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 0f;
        }

        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            float s = EaseOutBack(p);
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].localScale = Vector3.Scale(initialScales[i], Vector3.one * s);
                CanvasGroup cg = panels[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = p;
            }
            yield return null;
        }

        for (int i = 0; i < panels.Length; i++)
        {
            panels[i].localScale = initialScales[i];
            CanvasGroup cg = panels[i].GetComponent<CanvasGroup>();
            if (cg != null) cg.alpha = 1f;
        }
        anim = null;
    }

    /// <summary>收回动画：缩小到 0 后隐藏</summary>
    private IEnumerator Collapse()
    {
        float t = 0f;
        while (t < duration)
        {
            t += Time.deltaTime;
            float p = Mathf.Clamp01(t / duration);
            for (int i = 0; i < panels.Length; i++)
            {
                panels[i].localScale = Vector3.Scale(initialScales[i], Vector3.one * (1f - p));
                CanvasGroup cg = panels[i].GetComponent<CanvasGroup>();
                if (cg != null) cg.alpha = 1f - p;
            }
            yield return null;
        }

        foreach (var p in panels)
            p.gameObject.SetActive(false);
        anim = null;
    }

    /// <summary>回弹缓动（back ease out），0->1 会先冲过头一点再回落</summary>
    private static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f;
        const float c3 = c1 + 1f;
        return 1f + c3 * Mathf.Pow(t - 1f, 3f) + c1 * Mathf.Pow(t - 1f, 2f);
    }
}
