using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI.Yard;

namespace WildCourt.UI
{
    /// <summary>
    /// 公告弹窗：全屏半透明遮罩 + 居中公告卡（标题 / 日期 / 可滚动内容），仅通过关闭按钮（×）关闭。
    /// 支持两种承载方式：
    /// 1. 预制体模式（推荐）：公告弹窗挂在登录界面预制体中，四个 UI 引用（标题 / 日期 /
    ///    正文 / 关闭按钮）在 Inspector 序列化挂载，Awake 校验齐全后复用实例并拉取公告填充。
    /// 2. 纯代码模式：场景中无预制体实例时，由 Build 动态创建整套布局。
    /// 由 LoginController.Start 在进入登录界面时触发，免登录请求 GET /api/v1/announcements
    /// （AuthMiddleware 已放行），仅展示已发布公告的最新一条。无公告或失败时静默不弹。
    /// </summary>
    public class AnnouncementPopup : MonoBehaviour
    {
        private static AnnouncementPopup _instance;

        [SerializeField] private Text _titleText;
        [SerializeField] private Text _dateText;
        [SerializeField] private Text _contentText;
        [SerializeField] private Button _closeButton;

        private bool _isPrefab;    // 预制体模式：隐藏复用而非销毁
        private bool _eventsBound; // 关闭事件已绑定（防重复 AddListener）

        /// <summary>打开公告弹窗：优先复用场景中已有的实例（预制体模式），否则动态创建。</summary>
        public static void Open(Transform canvasRoot)
        {
            // 场景中已存在公告弹窗（如登录界面预制体里的实例）→ 复用
            var existing = FindObjectOfType<AnnouncementPopup>(true);
            if (existing != null)
            {
                _instance = existing;
                existing.FetchAndShow();
                return;
            }

            if (_instance != null)
            {
                _instance.FetchAndShow();
                return;
            }

            // 登录场景未挂载 NetManager，先自举（免登录公开接口，不注入 token）
            AuthService.EnsureNetManager();

            NetManager.Instance.Get<AnnouncementDto[]>(ApiPaths.Announcements,
                onSuccess: resp => OnFetched(canvasRoot, resp.data),
                onError: (code, msg) => Debug.LogWarning($"[Announcement] 公告拉取失败: {msg}"));
        }

        /// <summary>关闭公告弹窗：预制体实例隐藏复用，动态创建实例销毁。</summary>
        public static void Close()
        {
            if (_instance == null) return;
            if (_instance._isPrefab)
            {
                _instance.gameObject.SetActive(false);
            }
            else
            {
                Destroy(_instance.gameObject);
            }
            _instance = null;
        }

        private void Awake()
        {
            // 序列化挂载模式：四个 UI 引用在预制体 Inspector 上已指定，齐全才视为预制体模式；
            // 任一缺失（如纯代码 AddComponent 实例）则交给 Build 兜底。
            if (_titleText != null && _dateText != null && _contentText != null && _closeButton != null)
            {
                _isPrefab = true;
                BindCloseEvents();
                // 预制体根节点默认激活，先隐藏空壳，等待服务器数据返回后再显示
                gameObject.SetActive(false);
            }
            else
            {
                _isPrefab = false;
            }
        }

        /// <summary>拉取最新公告并填充显示（复用已有实例时调用）。</summary>
        public void FetchAndShow()
        {
            // 兜底：非预制体模式（层级缺失）时按代码动态创建布局
            if (!_isPrefab) Build();

            AuthService.EnsureNetManager();

            NetManager.Instance.Get<AnnouncementDto[]>(ApiPaths.Announcements,
                onSuccess: resp => OnData(resp.data),
                onError: (code, msg) =>
                {
                    Debug.LogWarning($"[Announcement] 公告拉取失败: {msg}");
                    if (_isPrefab) gameObject.SetActive(false); // 拉取失败时隐藏空壳
                });
        }

        private void OnData(AnnouncementDto[] list)
        {
            if (list == null || list.Length == 0)
            {
                if (_isPrefab) gameObject.SetActive(false); // 无已发布公告：保持隐藏
                return;
            }

            // 先激活再填充：保证 Fill 里的协程（延迟重建布局）能在激活状态下执行
            gameObject.SetActive(true);
            transform.SetAsLastSibling(); // 置顶显示
            Fill(list[0]); // 接口已按 ID 倒序，取最新一条
        }

        private static void OnFetched(Transform canvasRoot, AnnouncementDto[] list)
        {
            if (_instance != null) return;
            if (list == null || list.Length == 0) return; // 无已发布公告，不弹

            var go = YardUiKit.CreateUiObject("AnnouncementPopup", canvasRoot);
            var panel = go.AddComponent<AnnouncementPopup>();
            _instance = panel;
            panel.Build();
            panel.Fill(list[0]);
        }

        /// <summary>绑定关闭按钮点击事件（先移除再添加，保证幂等）。</summary>
        private void BindCloseEvents()
        {
            if (_eventsBound) return;
            _eventsBound = true;
            if (_closeButton != null)
            {
                _closeButton.onClick.RemoveListener(OnCloseClick);
                _closeButton.onClick.AddListener(OnCloseClick);
            }
        }

        private void Build()
        {
            // ---- 全屏遮罩（仅视觉，不接收关闭事件） ----
            YardUiKit.SetAnchors(gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            YardUiKit.AddImage(gameObject, new Color(0f, 0f, 0f, 0.55f));

            // ---- 公告卡 ----
            var card = YardUiKit.CreateUiObject("Card", transform);
            YardUiKit.SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-230f, -280f), new Vector2(230f, 260f));
            YardUiKit.AddImage(card, new Color(0.18f, 0.21f, 0.26f, 0.98f));

            // 关闭按钮（×）
            var closeGo = YardUiKit.CreateUiObject("Close", card.transform);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-8f, -8f);
            closeRt.sizeDelta = new Vector2(36f, 36f);
            _closeButton = YardUiKit.AddButton(closeGo, "×", OnCloseClick);
            _closeButton.targetGraphic = closeGo.GetComponent<Image>();

            // 标题
            var titleGo = YardUiKit.CreateUiObject("Title", card.transform);
            YardUiKit.SetAnchors(titleGo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, -72f), new Vector2(200f, -36f));
            _titleText = YardUiKit.AddText(titleGo, "", 22, Color.white);

            // 日期
            var dateGo = YardUiKit.CreateUiObject("Date", card.transform);
            YardUiKit.SetAnchors(dateGo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f),
                new Vector2(-200f, -104f), new Vector2(200f, -84f));
            _dateText = YardUiKit.AddText(dateGo, "", 12, new Color(0.62f, 0.68f, 0.74f));

            // 内容滚动区
            var scrollGo = YardUiKit.CreateUiObject("Scroll", card.transform);
            YardUiKit.SetAnchors(scrollGo, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f),
                new Vector2(-215f, -30f), new Vector2(215f, 150f));
            BuildScrollContent(scrollGo);

            BindCloseEvents();
        }

        private void BuildScrollContent(GameObject scrollGo)
        {
            // 滚动区本身必须有可射线检测的 Graphic（透明 Image），
            // 否则 ScrollRect 收不到拖拽 / 滚轮事件，超长内容将无法滚动查看。
            var scrollImg = YardUiKit.AddImage(scrollGo, new Color(0f, 0f, 0f, 0.01f));

            // viewport：裁剪子内容
            var viewport = YardUiKit.CreateUiObject("Viewport", scrollGo.transform);
            YardUiKit.SetStretch(viewport);
            viewport.AddComponent<RectMask2D>();

            // content：高度随文本自适应
            var content = YardUiKit.CreateUiObject("Content", viewport.transform);
            var contentRt = content.GetComponent<RectTransform>();
            contentRt.anchorMin = new Vector2(0f, 1f);
            contentRt.anchorMax = new Vector2(1f, 1f);
            contentRt.pivot = new Vector2(0.5f, 1f);
            contentRt.anchoredPosition = Vector2.zero;
            contentRt.sizeDelta = new Vector2(0f, 0f);

            var fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // 正文
            _contentText = content.AddComponent<Text>();
            var font = YardUiKit.LoadFont();
            if (font != null) _contentText.font = font;
            _contentText.fontSize = 15;
            _contentText.color = new Color(0.88f, 0.90f, 0.93f);
            _contentText.alignment = TextAnchor.UpperLeft;
            _contentText.horizontalOverflow = HorizontalWrapMode.Wrap;
            _contentText.verticalOverflow = VerticalWrapMode.Overflow;
            _contentText.raycastTarget = false;

            // 指定换行宽度，使 ContentSizeFitter 按此宽度计算高度
            var le = content.AddComponent<LayoutElement>();
            le.preferredWidth = 410f;

            // ScrollRect 组装
            var scroll = scrollGo.AddComponent<ScrollRect>();
            scroll.viewport = viewport.GetComponent<RectTransform>();
            scroll.content = contentRt;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 24f;
        }

        private void Fill(AnnouncementDto dto)
        {
            if (dto == null) return;
            if (_titleText != null) _titleText.text = string.IsNullOrEmpty(dto.title) ? "系统公告" : dto.title;
            if (_dateText != null) _dateText.text = FormatDate(dto.publishedAt ?? dto.createdAt);
            if (_contentText != null)
            {
                _contentText.text = string.IsNullOrEmpty(dto.content) ? "暂无内容" : dto.content;

                // 动态字体（LegacyRuntime.ttf）的中文/符号字形需按需生成。
                // 提前请求字形，避免 ContentSizeFitter 首帧量高时因字形缺失而算矮。
                var font = _contentText.font;
                if (font != null && !string.IsNullOrEmpty(_contentText.text))
                {
                    font.RequestCharactersInTexture(_contentText.text, _contentText.fontSize, _contentText.fontStyle);
                }
                StartCoroutine(RebuildLayoutLate());
            }
        }

        /// <summary>延迟一帧后强制重建 content 布局：确保 ContentSizeFitter 基于已生成的字形重新量高，滚动区可滚到底。</summary>
        private IEnumerator RebuildLayoutLate()
        {
            yield return null; // 等待字形生成与首帧布局
            if (_contentText == null) yield break;
            LayoutRebuilder.ForceRebuildLayoutImmediate(_contentText.rectTransform);
        }

        private static string FormatDate(string iso)
        {
            if (string.IsNullOrEmpty(iso)) return string.Empty;
            return iso.Length >= 10 ? iso.Substring(0, 10) : iso; // yyyy-MM-dd
        }

        // 用命名方法替代匿名 Lambda（Rule 7：避免临时闭包分配，且保证 OnDestroy 可移除）
        private void OnCloseClick() => Close();

        private void OnDestroy()
        {
            if (_closeButton != null) _closeButton.onClick.RemoveListener(OnCloseClick);
            if (_instance == this) _instance = null;
        }
    }
}
