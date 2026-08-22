using System;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.Fishing
{
    /// <summary>拉力面板结算结果类型。</summary>
    public enum ResultKind { Success, Escaped, LineBroken }

    /// <summary>
    /// 收线面板：收线进度条 + 连点计数 + 结局表现。
    /// 进度条分色：0~50% 黄 / 50~100% 绿；上钩提示持续显示直至收线结束。
    /// 显示/隐藏用 DOFade + DOScale（0.15s），禁止瞬切。
    /// </summary>
    public class TensionPanelController : MonoBehaviour
    {
        [SerializeField] Text txtTitle;
        [SerializeField] Slider sliderTension;        // 旧张力滑条（新流程隐藏）
        [SerializeField] Image dangerZone;            // 旧挣扎遮罩（新流程隐藏）
        [SerializeField] Text txtTensionState;        // 连点计数文本
        [SerializeField] Text txtCatchProgress;       // "收线进度 X%"
        [SerializeField] Slider sliderCatchProgress;  // 收线进度条
        [SerializeField] Button btnReel;              // 旧收线按钮（新流程隐藏）

        private CanvasGroup _group;
        private Color _normalTitleColor = Color.black;

        // 进度条分色
        private static readonly Color ColorYellow = new Color(0.95f, 0.75f, 0.25f, 1f);
        private static readonly Color ColorGreen = new Color(0.36f, 0.78f, 0.42f, 1f);

        private void Awake()
        {
            _group = GetComponent<CanvasGroup>();
            if (_group == null) _group = gameObject.AddComponent<CanvasGroup>();
            if (txtTitle != null) _normalTitleColor = txtTitle.color;
            if (sliderTension != null) sliderTension.value = 0f;
            if (sliderCatchProgress != null) sliderCatchProgress.value = 0f;

            ApplyStyle(UIStyleService.Current);
        }

        /// <summary>Step11：从 DT_UIStyle 注入字号与尺寸（数据驱动，禁止硬编码）。</summary>
        public void ApplyStyle(UIStyleConfig s)
        {
            if (txtTitle != null) txtTitle.fontSize = s.titleSize;
            if (txtTensionState != null) txtTensionState.fontSize = s.tensionStateSize;
            if (txtCatchProgress != null) txtCatchProgress.fontSize = s.catchProgressSize;
            if (btnReel != null)
                ((RectTransform)btnReel.transform).sizeDelta = new Vector2(s.reelButtonSize, s.reelButtonSize);
            if (sliderTension != null)
            {
                var r = (RectTransform)sliderTension.transform;
                r.sizeDelta = new Vector2(r.sizeDelta.x, s.tensionSliderHeight);
            }
            if (sliderCatchProgress != null)
            {
                var r = (RectTransform)sliderCatchProgress.transform;
                r.sizeDelta = new Vector2(r.sizeDelta.x, s.catchSliderHeight);
            }
        }

        /// <summary>显示面板并重置 UI。新流程鱼种收线结束才确定，fish 传 null。</summary>
        public void Show(FishConfig fish)
        {
            gameObject.SetActive(true);
            transform.DOKill();
            _group.alpha = 0f;
            transform.localScale = Vector3.one * 0.9f;
            _group.DOFade(1f, 0.15f).SetLink(gameObject);
            transform.DOScale(Vector3.one, 0.15f).SetLink(gameObject);

            if (txtTitle != null)
            {
                txtTitle.text = "上钩了！快收线！";
                txtTitle.color = _normalTitleColor;
            }
            // 新流程无张力体系：隐藏旧张力滑条/挣扎遮罩/独立收线按钮，进度只由主按钮驱动
            if (sliderTension != null) sliderTension.gameObject.SetActive(false);
            if (dangerZone != null) dangerZone.gameObject.SetActive(false);
            if (btnReel != null) btnReel.gameObject.SetActive(false);

            SetCatchProgress(0f);
            SetReelCounter(0, Mathf.Max(1, Mathf.CeilToInt(100f / 20f)));
        }

        public void Hide()
        {
            transform.DOKill();
            _group.DOFade(0f, 0.15f).SetLink(gameObject).OnComplete(() => gameObject.SetActive(false));
        }

        /// <summary>旧张力接口保留（兼容），新流程不再调用。</summary>
        public void SetTension(float v) { }

        /// <summary>刷新收线进度条，并按 0~50 黄 / 50~100 绿分色。</summary>
        public void SetCatchProgress(float v)
        {
            v = Mathf.Clamp(v, 0f, 100f);
            if (sliderCatchProgress != null)
            {
                sliderCatchProgress.value = v;
                var fill = sliderCatchProgress.fillRect != null
                    ? sliderCatchProgress.fillRect.GetComponent<Image>() : null;
                if (fill != null)
                    fill.color = v < 50f ? ColorYellow : ColorGreen;
            }
            if (txtCatchProgress != null) txtCatchProgress.text = $"收线进度 {(int)v}%";
        }

        /// <summary>连点计数："连点 3/5"。</summary>
        public void SetReelCounter(int current, int required)
        {
            current = Mathf.Clamp(current, 0, required);
            if (txtTensionState != null)
                txtTensionState.text = $"连点 {current}/{required}";
        }

        /// <summary>旧挣扎接口保留（兼容），新流程不再调用。</summary>
        public void SetStruggling(bool on) { }

        /// <summary>
        /// 播放结局表现，播完回调 onComplete。
        /// 成功：进度条闪绿 + 缩放脉冲；逃脱：整体淡出。
        /// </summary>
        public void PlayResult(ResultKind kind, Action onComplete = null)
        {
            Sequence seq = DOTween.Sequence().SetLink(gameObject);
            switch (kind)
            {
                case ResultKind.Success:
                    var fillImg = sliderCatchProgress != null && sliderCatchProgress.fillRect != null
                        ? sliderCatchProgress.fillRect.GetComponent<Image>() : null;
                    if (fillImg != null)
                        seq.Append(fillImg.DOColor(Color.green, 0.3f).SetLink(gameObject));
                    seq.Append(transform.DOPunchScale(Vector3.one * 0.15f, 0.4f, 4, 0.5f).SetLink(gameObject));
                    break;
                case ResultKind.Escaped:
                    seq.Append(_group.DOFade(0f, 0.5f).SetLink(gameObject));
                    break;
                case ResultKind.LineBroken:
                    seq.Append(((RectTransform)transform).DOShakeAnchorPos(0.5f, 10f, 16, 90f, false, true).SetLink(gameObject));
                    seq.Append(_group.DOFade(0f, 0.4f).SetLink(gameObject));
                    break;
            }
            seq.AppendCallback(() =>
            {
                gameObject.SetActive(false);
                onComplete?.Invoke();
            });
        }
    }
}
