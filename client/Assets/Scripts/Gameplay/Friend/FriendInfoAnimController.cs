using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂到 FriendsInformation 节点：控制其 Legacy 动画的播放与复位。
/// - PlayAnimation()：播放动画（由 Menu 按钮点击触发）。
/// - 点击 FriendsInformation 之外的区域（且不在 Menu 上）时，动画恢复默认。
/// </summary>
public class FriendInfoAnimController : MonoBehaviour
{
    [Tooltip("Menu 按钮的 RectTransform（点击它播放动画，不应被误判为外部点击而复位）")]
    [SerializeField] private RectTransform menuRect;

    private Animation _anim;
    private RectTransform _rect;

    void Awake()
    {
        _anim = GetComponent<Animation>();
        _rect = GetComponent<RectTransform>();
    }

    /// <summary>播放动画（从初始帧开始）。</summary>
    public void PlayAnimation()
    {
        if (_anim == null) return;
        _anim.Rewind();
        _anim.Play();
    }

    /// <summary>恢复动画默认（回到初始帧并停止）。</summary>
    public void ResetAnimation()
    {
        if (_anim == null) return;
        _anim.Rewind();
        _anim.Sample();
        _anim.Stop();
    }

    void Update()
    {
        // 只处理点击按下（PC 鼠标左键 / 移动端触摸），不处理悬停
        if (!Input.GetMouseButtonDown(0)) return;
        if (_rect == null) return;

        Vector2 pointer = Input.mousePosition;
        bool inInfo = RectTransformUtility.RectangleContainsScreenPoint(_rect, pointer);
        bool inMenu = menuRect != null && RectTransformUtility.RectangleContainsScreenPoint(menuRect, pointer);
        if (!inInfo && !inMenu)
        {
            // 点击了 FriendsInformation 之外的区域（且不是 Menu）→ 动画恢复默认
            ResetAnimation();
        }
    }
}
