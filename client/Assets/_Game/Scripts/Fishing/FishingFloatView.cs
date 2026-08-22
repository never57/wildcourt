using DG.Tweening;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>浮漂视图：显示时轻微上下浮动循环。</summary>
    public class FishingFloatView : MonoBehaviour
    {
        public void Show()
        {
            gameObject.SetActive(true);
            transform.DOKill();
            transform.localScale = Vector3.one;
            transform.localPosition = Vector3.zero;
            transform.DOLocalMoveY(3f, 0.6f).SetLoops(-1, LoopType.Yoyo).SetEase(Ease.InOutSine).SetLink(gameObject);
        }

        public void Hide()
        {
            transform.DOKill();
            gameObject.SetActive(false);
        }
    }
}
