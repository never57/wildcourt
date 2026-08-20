using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 挂到 FriendsInformation 节点：控制其 Legacy 动画的播放与复位。
/// - PlayAnimation()：播放动画（由 Menu 按钮点击触发）。
/// - 点击 FriendsInformation 之外的区域（且不在 Menu 上）时，动画恢复默认。
/// 恢复时显式把 UI 位置/缩放等还原到初始值，不依赖动画 clip 的 time=0 关键帧。
/// </summary>
public class FriendInfoAnimController : MonoBehaviour
{
    [Tooltip("Menu 按钮的 RectTransform（点击它播放动画，不应被误判为外部点击而复位）")]
    [SerializeField] private RectTransform menuRect;

    private Animation _anim;
    private RectTransform _rect;
    private Canvas _canvas;
    private Camera _eventCamera;

    // 记录初始 UI 变换，复位时显式还原（动画 clip 可能没有 time=0 关键帧）
    private Vector3 _initLocalPos;
    private Vector3 _initLocalScale;
    private Quaternion _initLocalRot;
    private Vector2 _initAnchoredPos;
    private Vector2 _initSizeDelta;

    void Awake()
    {
        _anim = GetComponent<Animation>();
        _rect = GetComponent<RectTransform>();
        _canvas = GetComponentInParent<Canvas>();
        // Overlay 模式 worldCamera 为 null；Camera 模式取世界相机
        _eventCamera = _canvas != null ? _canvas.worldCamera : null;

        _initLocalPos = transform.localPosition;
        _initLocalScale = transform.localScale;
        _initLocalRot = transform.localRotation;
        if (_rect != null)
        {
            _initAnchoredPos = _rect.anchoredPosition;
            _initSizeDelta = _rect.sizeDelta;
        }
    }

    /// <summary>播放动画（从初始帧开始）。</summary>
    public void PlayAnimation()
    {
        if (_anim == null) return;
        _anim.Rewind();
        _anim.Play();
    }

    /// <summary>恢复动画默认：回到初始帧并停止，同时显式还原初始 UI 变换。</summary>
    public void ResetAnimation()
    {
        if (_anim != null)
        {
            _anim.Rewind();
            _anim.Sample();
            _anim.Stop();
        }
        RestoreInitialTransform();
    }

    private void RestoreInitialTransform()
    {
        transform.localPosition = _initLocalPos;
        transform.localScale = _initLocalScale;
        transform.localRotation = _initLocalRot;
        if (_rect != null)
        {
            _rect.anchoredPosition = _initAnchoredPos;
            _rect.sizeDelta = _initSizeDelta;
        }
    }

    void Update()
    {
        // 只处理点击按下（PC 鼠标左键 / 移动端触摸），不处理悬停
        if (!Input.GetMouseButtonDown(0)) return;
        if (_rect == null) return;

        Vector2 pointer = Input.mousePosition;
        bool inInfo = IsPointIn(_rect, pointer);
        bool inMenu = menuRect != null && IsPointIn(menuRect, pointer);
        if (!inInfo && !inMenu)
        {
            // 点击了 FriendsInformation 之外的区域（且不是 Menu）→ 动画恢复默认
            ResetAnimation();
        }
    }

    /// <summary>判断屏幕坐标是否落在某个 RectTransform 内（兼容 Overlay / Camera 两种 Canvas）。</summary>
    private bool IsPointIn(RectTransform rt, Vector2 screenPoint)
    {
        return RectTransformUtility.RectangleContainsScreenPoint(rt, screenPoint, _eventCamera);
    }
}
