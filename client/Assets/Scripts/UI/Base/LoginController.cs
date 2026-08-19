using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI.Yard;
using WildCourt.UI.Farm;
using WildCourt.UI.Letters;
using WildCourt.UI.User;

namespace WildCourt.UI
{
    /// <summary>
    /// 登录/注册控制器。
    /// 负责：面板切换、输入校验、调用 AuthService 发起网络请求、登录成功跳转主场景。
    /// 挂载在 LoginCanvas 上，由 start 场景序列化赋值引用。
    /// </summary>
    public class LoginController : MonoBehaviour
    {
        [Header("面板")]
        public GameObject loginPanel;
        public GameObject registerPanel;

        [Header("登录输入")]
        public InputField loginUsernameInput;
        public InputField loginPasswordInput;

        [Header("注册输入")]
        public InputField regUsernameInput;
        public InputField regPasswordInput;
        public InputField regConfirmInput;

        [Header("按钮")]
        public Button loginButton;
        public Button registerButton;
        public Button gotoRegisterLink;
        public Button gotoLoginLink;

        [Header("状态提示")]
        public Text statusText;

        [Header("登录成功菜单（LoginUI 实例内节点）")]
        [Tooltip("登录表单容器，登录成功后隐藏")]
        public GameObject loginInformation;
        [Tooltip("开始游戏按钮（Start 节点），登录成功后显示，点击进入主场景")]
        public Button startButton;
        [Tooltip("退出登录容器节点（exit），登录成功后整体显示，内含 exitButton 与 UserName")]
        public GameObject exitNode;
        [Tooltip("退出登录按钮（exit/exitButton），点击退出登录")]
        public Button exitButton;
        [Tooltip("显示玩家昵称的文本框（exit/UserName，TMP）")]
        public TMP_Text userNameText;

        [Header("跳转场景名")]
        [Tooltip("登录成功后跳转到的场景")]
        public string gameSceneName = "MainScene";

        [Header("预加载")]
        [Tooltip("拖入 YardMainUI Prefab，登录成功后注入 YardMainUI.YardMainUIPrefab（主 UI 框架，支持热更）")]
        public GameObject yardMainUiPrefab;

        [Tooltip("拖入 YardPanel Prefab，登录成功后注入 YardPanel.YardPanelPrefab（建造面板，点击建造按钮时打开，支持热更）")]
        public GameObject yardPanelPrefab;

        [Tooltip("拖入 FarmPanel Prefab，登录成功后注入 FarmPanel.FarmPanelPrefab（支持热更）")]
        public GameObject farmPanelPrefab;

        [Tooltip("拖入 MailPanel Prefab，登录成功后注入 MailPanel.MailPanelPrefab（信箱面板，点击庭院 mail 按钮打开，支持热更）")]
        public GameObject mailPanelPrefab;

        [Tooltip("拖入 UserPanel Prefab，登录成功后注入 UserPanel.UserPanelPrefab（玩家信息面板，点击庭院 editor 按钮打开，支持热更）")]
        public GameObject userPanelPrefab;

        private AuthService _auth;

        private void Start()
        {
            _auth = FindObjectOfType<AuthService>();
            if (_auth == null)
            {
                _auth = gameObject.AddComponent<AuthService>();
            }

            loginButton.onClick.AddListener(OnLoginClick);
            registerButton.onClick.AddListener(OnRegisterClick);
            gotoRegisterLink.onClick.AddListener(OnGotoRegister);
            gotoLoginLink.onClick.AddListener(OnGotoLogin);

            // 进入登录界面即弹出公告（公开接口免登录；无已发布公告或失败时静默不弹）
            AnnouncementPopup.Open(transform);

            // 绑定登录成功菜单按钮事件（节点在 LoginUI prefab 内通过 Inspector 拖拽绑定）
            if (startButton != null) startButton.onClick.AddListener(OnStartClick);
            if (exitButton != null) exitButton.onClick.AddListener(OnExitClick);

            // 初始状态：仅显示登录表单，隐藏 start / exit
            ShowLoginMenu(false);

            // 自动登录：本机若记住过登录（token 有效期内）则免输入直接进入已登录菜单
            TryAutoLogin();
        }

        /// <summary>
        /// 自动登录：读取本地持久化的会话 token，带 token 调 GET /user/profile 校验。
        /// 有效则直接进入「已登录菜单」（显示 start/exit + 昵称），免输入账号密码；失效则清除并走正常登录。
        /// </summary>
        private void TryAutoLogin()
        {
            var net = NetManager.Instance;
            if (net == null) return;

            var savedToken = net.RestoreToken();
            if (string.IsNullOrEmpty(savedToken)) return;

            net.SetToken(savedToken);   // 注入 token，后续请求自动带 Bearer

            // 用 token 调 profile 校验会话是否有效
            net.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp =>
                {
                    if (resp == null || resp.data == null)
                    {
                        OnAutoLoginFail();
                        return;
                    }
                    // token 有效：进入已登录菜单，并同步昵称
                    ShowLoginMenu(true);
                    ClearStatus();
                    // 自动登录同样需要注入庭院/农场等预制体，否则进主场景后 YardMainUI/FarmPanel 未注入无法创建
                    PreloadYardPanel();
                    AvatarManager.EnsureCreated();
                    AvatarManager.Instance.OnProfileReady(() =>
                    {
                        if (userNameText != null && AvatarManager.Instance != null)
                        {
                            userNameText.text = AvatarManager.Instance.MyNickname ?? resp.data.nickname;
                        }
                    });
                },
                (code, msg) => OnAutoLoginFail());
        }

        private void OnAutoLoginFail()
        {
            // 会话失效（过期/退出过）：清除本地 token，走正常登录界面
            if (NetManager.Instance != null) NetManager.Instance.ClearSession();
            if (AvatarManager.Instance != null) AvatarManager.Instance.ClearProfile();
            if (userNameText != null) userNameText.text = "";
            ShowLoginMenu(false);
        }

        private void OnStartClick()
        {
            // 点击开始游戏才进入主场景
            LoadGameScene();
        }

        private void OnExitClick()
        {
            // 退出登录：清空会话 + 缓存 + 昵称文本，恢复登录界面（下次登录成功会重新拉取 profile）
            if (NetManager.Instance != null) NetManager.Instance.ClearSession();
            if (AvatarManager.Instance != null) AvatarManager.Instance.ClearProfile();
            if (userNameText != null) userNameText.text = "";
            ClearInputs();
            ClearStatus();
            ShowLoginMenu(false);
            SetButtonsInteractable(true);
        }

        /// <summary>
        /// 切换登录成功菜单显隐。true=显示 start/exit、隐藏登录表单；false=隐藏 start/exit、显示登录表单。
        /// </summary>
        private void ShowLoginMenu(bool loggedIn)
        {
            if (loginInformation) loginInformation.SetActive(!loggedIn);
            if (startButton) startButton.gameObject.SetActive(loggedIn);
            if (exitNode) exitNode.SetActive(loggedIn);
        }

        private void OnDestroy()
        {
            if (loginButton) loginButton.onClick.RemoveListener(OnLoginClick);
            if (registerButton) registerButton.onClick.RemoveListener(OnRegisterClick);
            if (gotoRegisterLink) gotoRegisterLink.onClick.RemoveListener(OnGotoRegister);
            if (gotoLoginLink) gotoLoginLink.onClick.RemoveListener(OnGotoLogin);
        }

        // 用命名方法替代匿名 Lambda（Rule 7：避免临时闭包分配，且保证 OnDestroy 可移除）
        private void OnGotoRegister() => SwitchPanel(false);
        private void OnGotoLogin() => SwitchPanel(true);

        /// <summary>
        /// 切换到登录面板（true）或注册面板（false）。
        /// </summary>
        private void SwitchPanel(bool showLogin)
        {
            ClearInputs();
            ClearStatus();
            loginPanel.SetActive(showLogin);
            registerPanel.SetActive(!showLogin);
        }

        // ---- 登录 ----

        private void OnLoginClick()
        {
            ClearStatus();
            var username = loginUsernameInput.text.Trim();
            var password = loginPasswordInput.text;

            if (!ValidateUsername(username)) return;
            if (!ValidatePassword(password)) return;

            SetButtonsInteractable(false);
            ShowStatus("正在登录...", Color.white);

            _auth.Login(username, password,
                onSuccess: token =>
                {
                    ShowStatus("登录成功", new Color(0.4f, 0.8f, 0.4f));
                    // 初始化全局头像缓存（拉取自己的 profile，缓存头像 URL 与昵称）
                    AvatarManager.EnsureCreated();
                    PreloadYardPanel();
                    // 隐藏登录表单，显示 start / exit，并等待 profile 拉取后把昵称显示到 UserName
                    ShowLoginMenu(true);
                    // 登录成功后清除状态提示（菜单已表示登录成功，不再常驻「登录成功」文字）
                    ClearStatus();
                    AvatarManager.Instance.OnProfileReady(() =>
                    {
                        if (userNameText != null && AvatarManager.Instance != null)
                        {
                            userNameText.text = AvatarManager.Instance.MyNickname ?? username;
                        }
                    });
                },
                onError: err =>
                {
                    ShowStatus(err, new Color(0.95f, 0.35f, 0.25f));
                    SetButtonsInteractable(true);
                });
        }

        // ---- 注册 ----

        private void OnRegisterClick()
        {
            ClearStatus();
            var username = regUsernameInput.text.Trim();
            var password = regPasswordInput.text;
            var confirm = regConfirmInput.text;

            if (!ValidateUsername(username)) return;
            if (!ValidatePassword(password)) return;
            if (password != confirm)
            {
                ShowStatus("两次密码不一致", new Color(0.95f, 0.35f, 0.25f));
                return;
            }

            SetButtonsInteractable(false);
            ShowStatus("正在注册...", Color.white);

            _auth.Register(username, password,
                onSuccess: () =>
                {
                    ShowStatus("注册成功，请登录", new Color(0.4f, 0.8f, 0.4f));
                    SetButtonsInteractable(true);
                    // 自动切回登录面板并填入账号
                    loginUsernameInput.text = username;
                    SwitchPanel(true);
                },
                onError: err =>
                {
                    ShowStatus(err, new Color(0.95f, 0.35f, 0.25f));
                    SetButtonsInteractable(true);
                });
        }

        // ---- 校验 ----

        private bool ValidateUsername(string username)
        {
            if (string.IsNullOrEmpty(username))
            {
                ShowStatus("请输入账号", new Color(0.95f, 0.35f, 0.25f));
                return false;
            }
            if (username.Length < 4 || username.Length > 20)
            {
                ShowStatus("账号长度须为 4~20 位", new Color(0.95f, 0.35f, 0.25f));
                return false;
            }
            if (!Regex.IsMatch(username, @"^[a-zA-Z0-9_]+$"))
            {
                ShowStatus("账号只能包含字母、数字和下划线", new Color(0.95f, 0.35f, 0.25f));
                return false;
            }
            return true;
        }

        private bool ValidatePassword(string password)
        {
            if (string.IsNullOrEmpty(password))
            {
                ShowStatus("请输入密码", new Color(0.95f, 0.35f, 0.25f));
                return false;
            }
            if (password.Length < 6 || password.Length > 20)
            {
                ShowStatus("密码长度须为 6~20 位", new Color(0.95f, 0.35f, 0.25f));
                return false;
            }
            return true;
        }

        // ---- 场景跳转 ----

        /// <summary>
        /// 注入庭院面板预制体引用：仅使用 Inspector 拖入的引用（支持热更）。
        /// 未拖入则不注入，YardPanel 切场景时视为资源损坏并退出游戏。
        /// </summary>
        private void PreloadYardPanel()
        {
            if (yardMainUiPrefab != null) YardMainUI.YardMainUIPrefab = yardMainUiPrefab;
            if (yardPanelPrefab != null) YardPanel.YardPanelPrefab = yardPanelPrefab;
            if (farmPanelPrefab != null) FarmPanel.FarmPanelPrefab = farmPanelPrefab;
            if (mailPanelPrefab != null) MailPanel.MailPanelPrefab = mailPanelPrefab;
            if (userPanelPrefab != null) UserPanel.UserPanelPrefab = userPanelPrefab;
        }

        /// <summary>
        /// 登录成功后加载主游戏场景（SampleScene）。
        /// </summary>
        private void LoadGameScene()
        {
            // 统一走 GameManager 的场景切换入口，便于后续扩展加载流程
            if (GameManager.Instance != null)
            {
                GameManager.Instance.LoadScene(gameSceneName);
            }
            else
            {
                SceneManager.LoadScene(gameSceneName);
            }
        }

        // ---- 工具 ----

        private void ClearInputs()
        {
            if (loginUsernameInput) loginUsernameInput.text = "";
            if (loginPasswordInput) loginPasswordInput.text = "";
            if (regUsernameInput) regUsernameInput.text = "";
            if (regPasswordInput) regPasswordInput.text = "";
            if (regConfirmInput) regConfirmInput.text = "";
        }

        private void ClearStatus()
        {
            if (statusText) statusText.text = "";
        }

        private void ShowStatus(string msg, Color color)
        {
            if (statusText)
            {
                statusText.text = msg;
                statusText.color = color;
            }
        }

        private void SetButtonsInteractable(bool enabled)
        {
            if (loginButton) loginButton.interactable = enabled;
            if (registerButton) registerButton.interactable = enabled;
        }
    }
}
