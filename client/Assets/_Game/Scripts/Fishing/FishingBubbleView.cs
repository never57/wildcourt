using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;

namespace Game.Fishing
{
    /// <summary>
    /// 状态气泡视图：普通态上下浮动循环，bite 态文案切换 + 缩放脉冲。
    /// </summary>
    public class FishingBubbleView : MonoBehaviour
    {
        [SerializeField] Text txt;

        public string CurrentText => txt != null ? txt.text : "";

        private void Awake()
        {
            // Step11：气泡字号由 DT_UIStyle 注入
            if (txt != null) txt.fontSize = UIStyleService.Current.bubbleSize;
        }

        public void SetBubble(string text, bool isBite)
        {
            gameObject.SetActive(true);
            if (txt != null) txt.text = text;
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            if (isBite)
            {
                transform.DOPunchScale(Vector3.one * 0.25f, 0.5f, 6, 0.4f).SetLink(gameObject);
            }
            else
            {
                transform.DOLocalMoveY(8f, 0.8f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
            }
        }

        public void Hide()
        {
            transform.DOKill();
            gameObject.SetActive(false);
        }
    }
}
