using DG.Tweening;
using UnityEngine;

namespace Game.Fishing
{
    /// <summary>鱼竿视图：PlayCast 甩竿动作。</summary>
    public class FishingRodView : MonoBehaviour
    {
        public RectTransform RodTipAnchor;

        public RectTransform RodTip => RodTipAnchor;

        public void PlayCast()
        {
            transform.DOKill();
            transform.DOPunchRotation(Vector3.forward * 12f, 0.4f, 2, 0.4f).SetLink(gameObject);
        }
    }
}
