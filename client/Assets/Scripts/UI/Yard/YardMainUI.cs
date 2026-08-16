using UnityEngine;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Gameplay.Yard;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI.Letters;
using WildCourt.UI.User;
using WildCourt.Utils;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 庭院主 UI 框架（挂在 YardMainUI.prefab 根节点）。
    /// 登录进入 MainScene 后实例化并常驻显示；庭院建造面板（YardPanel）不第一时间实例化，
    /// 点击“建造”（bulidsystem）按钮时才实例化打开。
    /// 玩家资料（余额/昵称等）在 YardPanel 打开后注入，保证其显示数据完整。
    /// </summary>
    public class YardMainUI : MonoBehaviour
    {
        public static YardMainUI Instance { get; private set; }

        /// <summary>跨场景预加载的 YardMainUI 预制体引用，由 LoginController 登录后注入（支持热更）。</summary>
        public static GameObject YardMainUIPrefab { get; set; }

        /// <summary>庭院建造面板预制体引用（转交 YardPanel.YardPanelPrefab）。</summary>
        public static GameObject YardPanelPrefab
        {
            get => YardPanel.YardPanelPrefab;
            set => YardPanel.YardPanelPrefab = value;
        }

        [Header("YardMainUI.prefab 内绑定")]
        [SerializeField] private Button _buildButton;      // bulidsystem 按钮
        [SerializeField] private Button _mailButton;       // mail 按钮，点击打开信箱（MailPanel）
        [SerializeField] private Button _friendsButton;    // friends 按钮，点击打开好友聊天（ChatManner）
        [SerializeField] private Button _editorButton;     // editor 按钮，点击打开玩家信息面板（UserPanel）

        [Header("玩家信息（playerinformation 内绑定）")]
        [SerializeField] private Image _playerInfoAvatarImage;   // playerinformation/Image：头像显示，与服务器头像同步

        private YardManager _yard;
        private YardPanel _panel;
        private UserProfileResponse _profile;

        /// <summary>入口：实例化 YardMainUI 预制体并初始化（预制体缺失则告警返回 null）。</summary>
        public static YardMainUI CreateOrInstantiate(YardManager yard)
        {
            if (YardMainUIPrefab == null)
            {
                Debug.LogWarning("[YardMainUI] 未注入 YardMainUI 预制体，跳过主 UI 创建");
                return null;
            }
            if (Instance != null)
            {
                Instance.BindYard(yard);
                return Instance;
            }
            var go = Instantiate(YardMainUIPrefab);
            var ui = go.GetComponent<YardMainUI>();
            if (ui == null)
            {
                Destroy(go);
                Debug.LogWarning("[YardMainUI] 预制体缺少 YardMainUI 组件");
                return null;
            }
            ui.BindYard(yard);
            ui.LoadProfile();
            return ui;
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

            // 订阅货币变化：邮箱领取附件等场景会广播，收到后重拉 profile 对齐权威余额
            EventBus.Subscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            // 订阅资料更新：UserPanel 改头像/昵称后广播，同步 playerinformation 头像
            EventBus.Subscribe<ProfileUpdatedEvent>(OnProfileUpdated);
        }

        /// <summary>货币变化（领取附件/购买等）：重拉玩家资料，保证建筑面板余额显示最新值。</summary>
        private void OnCurrencyChanged(CurrencyChangedEvent evt)
        {
            LoadProfile();
        }

        /// <summary>资料更新（UserPanel 改头像/昵称后广播）：同步 playerinformation 头像。</summary>
        private void OnProfileUpdated(ProfileUpdatedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.AvatarUrl))
            {
                AvatarLoader.LoadInto(_playerInfoAvatarImage, AvatarLoader.ToAbsoluteUrl(evt.AvatarUrl), this);
            }
        }

        private void BindYard(YardManager yard)
        {
            _yard = yard;
            if (_buildButton != null)
            {
                _buildButton.onClick.RemoveAllListeners();
                _buildButton.onClick.AddListener(OpenBuildPanel);
            }
            if (_mailButton != null)
            {
                _mailButton.onClick.RemoveAllListeners();
                _mailButton.onClick.AddListener(OpenMailPanel);
            }
            if (_friendsButton != null)
            {
                _friendsButton.onClick.RemoveAllListeners();
                _friendsButton.onClick.AddListener(OpenChat);
            }
            if (_editorButton != null)
            {
                _editorButton.onClick.RemoveAllListeners();
                _editorButton.onClick.AddListener(OpenUserPanel);
            }
            YardPanel.BackPressed -= OnBuildPanelClosed;
            YardPanel.BackPressed += OnBuildPanelClosed;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_buildButton != null) _buildButton.onClick.RemoveAllListeners();
            if (_mailButton != null) _mailButton.onClick.RemoveAllListeners();
            if (_friendsButton != null) _friendsButton.onClick.RemoveAllListeners();
            if (_editorButton != null) _editorButton.onClick.RemoveAllListeners();
            YardPanel.BackPressed -= OnBuildPanelClosed;
            EventBus.Unsubscribe<CurrencyChangedEvent>(OnCurrencyChanged);
            EventBus.Unsubscribe<ProfileUpdatedEvent>(OnProfileUpdated);
        }

        /// <summary>建造面板返回：隐藏建造面板，恢复显示主 UI。</summary>
        private void OnBuildPanelClosed()
        {
            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
            gameObject.SetActive(true);
        }

        /// <summary>拉取玩家资料，缓存后供 YardPanel 打开时注入（余额显示），并同步 playerinformation 头像。</summary>
        private void LoadProfile()
        {
            var net = NetManager.Instance;
            if (net == null) return;
            net.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp =>
                {
                    _profile = resp.data;
                    // 初始化/刷新时同步头像到 playerinformation
                    if (resp.data != null && !string.IsNullOrEmpty(resp.data.avatarUrl))
                    {
                        AvatarLoader.LoadInto(_playerInfoAvatarImage, AvatarLoader.ToAbsoluteUrl(resp.data.avatarUrl), this);
                    }
                },
                (code, msg) => Debug.LogWarning($"[YardMainUI] profile 加载失败: {msg}"));
        }

        /// <summary>点击“信箱”按钮：实例化打开信箱面板（MailPanel）。MailPanel 为独立 Canvas 弹窗，覆盖在庭院 UI 之上。</summary>
        private void OpenMailPanel()
        {
            var panel = MailPanel.CreateOrInstantiate();
            if (panel != null) panel.Open();
        }

        /// <summary>点击“好友”按钮：打开好友聊天窗口（ChatManner）。首次实例化 chat.prefab，之后复用。</summary>
        private void OpenChat()
        {
            ChatManner.CreateOrInstantiate();
        }

        /// <summary>点击“editor”按钮：实例化打开玩家信息面板（UserPanel）。</summary>
        private void OpenUserPanel()
        {
            var panel = UserPanel.CreateOrInstantiate();
            if (panel != null) panel.Open();
        }

        /// <summary>点击“建造”按钮：实例化打开庭院建造面板（YardPanel），注入已缓存资料，并隐藏主 UI。</summary>
        private void OpenBuildPanel()
        {
            if (_yard == null) return;
            if (_panel != null)
            {
                _panel.gameObject.SetActive(true);
                _panel.RefreshOnOpen();
                gameObject.SetActive(false);
                return;
            }
            _panel = YardPanel.CreateOrInstantiate(_yard);
            if (_panel != null && _profile != null)
            {
                _panel.OnProfileLoaded(_profile);
            }
            gameObject.SetActive(false);
        }
    }
}
