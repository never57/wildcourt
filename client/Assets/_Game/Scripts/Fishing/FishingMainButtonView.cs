using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.Fishing
{
    /// <summary>主操作按钮：随状态机切换文案/可用性/置灰，收线时每次点击抖动。</summary>
    public class FishingMainButtonView : MonoBehaviour
    {
        public Button Button;
        public Text TxtMain;
        public Text TxtSub;
        public Image Background;

        private FishingSessionController _controller;
        private Color _normalColor = Color.white;
        private RectTransform _rt;
        private RectTransform _parentRect;
        private float _lastFitWidth = -1f;

        private void Awake()
        {
            if (Background != null) _normalColor = Background.color;
            // Step11：主按钮文案字号由 DT_UIStyle 注入
            if (TxtMain != null) TxtMain.fontSize = UIStyleService.Current.mainButtonTextSize;
            if (TxtSub != null) TxtSub.fontSize = UIStyleService.Current.descSize;
            _rt = transform as RectTransform;
            if (transform.parent != null) _parentRect = transform.parent as RectTransform;
        }

        private const float RightInset = 24f;      // 右侧栏距屏幕右缘
        private const float RightBarWidth = 200f;  // 右侧栏宽度
        private const float MinGap = 30f;          // 要求的水平最小间距（参考分辨率像素）

        private void Update()
        {
            FitToRightBar();
        }

        /// <summary>
        /// 运行时水平自适应：保持 Bottom-Center 锚点，窄屏时自动左移，
        /// 保证抛竿按钮右边缘与右侧功能栏（右缘留 24、栏宽 200）间距 ≥ 30。
        /// 采用“只左钳、不右推”策略：SafeAreaController 等已把按钮左移时保持不动，
        /// 仅当按钮右边缘越界（间距不足）时左移；空间充足则保持居中。
        /// </summary>
        private void FitToRightBar()
        {
            EnsureCached();
            if (_rt == null || _parentRect == null) return;
            float width = _parentRect.rect.width;
            var p = _rt.anchoredPosition;
            float maxCenterX = ComputeMaxCenterX(width);
            if (Mathf.Abs(width - _lastFitWidth) >= 0.5f)
            {
                // 父级尺寸变化（分辨率/方向/安全区）：空间充足回到水平居中，不足则左移保证间距
                _lastFitWidth = width;
                p.x = Mathf.Min(0f, maxCenterX);
                _rt.anchoredPosition = p;
            }
            else if (p.x > maxCenterX)
            {
                // 被外部逻辑向右移动导致间距不足 → 强制左钳（不干扰 SafeArea 已左移的位置）
                p.x = maxCenterX;
                _rt.anchoredPosition = p;
            }
        }

        private void EnsureCached()
        {
            if (_rt == null) _rt = transform as RectTransform;
            if (_parentRect == null && transform.parent != null) _parentRect = transform.parent as RectTransform;
        }

        private float ComputeMaxCenterX(float width)
        {
            float half = _rt.rect.width > 0f ? _rt.rect.width * 0.5f : 140f;
            return Mathf.Min(0f, width * 0.5f - (RightInset + RightBarWidth + half + MinGap));
        }

        public void BindController(FishingSessionController c)
        {
            _controller = c;
            if (Button != null)
            {
                Button.onClick.RemoveAllListeners();
                Button.onClick.AddListener(() => _controller?.HandleMainButtonClick());
            }
        }

        public void SetState(FishingState state)
        {
            if (TxtMain != null)
            {
                TxtMain.text = state switch
                {
                    FishingState.Waiting => "等待",
                    FishingState.Biting => "收线！",
                    FishingState.Reeling => "收线！",
                    _ => "抛竿"
                };
            }
            if (TxtSub != null)
            {
                TxtSub.text = state switch
                {
                    FishingState.Idle => "消耗1鱼饵",
                    FishingState.Waiting => "静待鱼儿",
                    FishingState.Biting => "快！连续点击！",
                    FishingState.Reeling => "保持连点！",
                    FishingState.Success => "鱼获已入背包",
                    FishingState.Failed => "鱼已逃脱",
                    _ => ""
                };
            }
            if (Button != null)
                Button.interactable = state == FishingState.Idle
                                      || state == FishingState.Biting
                                      || state == FishingState.Reeling;
            if (Background != null)
            {
                Background.DOKill();
                if (state == FishingState.Waiting || state == FishingState.Success || state == FishingState.Failed)
                    Background.DOColor(new Color(0.7f, 0.7f, 0.7f, 1f), 0.2f).SetLink(gameObject);
                else
                    Background.DOColor(_normalColor, 0.2f).SetLink(gameObject);
            }
        }

        /// <summary>每次收线点击的抖动反馈（不改位置，仅缩放脉冲）。</summary>
        public void PlayClickPunch()
        {
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.DOPunchScale(Vector3.one * 0.12f, 0.18f, 4, 0.5f).SetLink(gameObject);
        }
    }
}
