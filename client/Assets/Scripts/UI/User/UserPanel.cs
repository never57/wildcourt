using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.User
{
    /// <summary>
    /// 玩家信息面板（挂在 UserPanel.prefab 根节点）。
    /// 由主界面 YardMainUI 的 editor 按钮实例化打开；Close 按钮关闭。
    /// 显示玩家 ID、庭院等级（= 玩家等级），并支持修改昵称：
    /// 点击 Editor 按钮激活 UserName 输入框编辑，失焦/回车后提交 PUT /user/nickname 保存。
    /// 子节点按名字自动查找（Close / Editor / ID / UserName / UserLevel），
    /// 也可通过序列化字段手动绑定覆盖。
    /// </summary>
    public class UserPanel : MonoBehaviour
    {
        public static UserPanel Instance { get; private set; }

        /// <summary>跨场景注入的 UserPanel 预制体（登录时注入，支持热更）。</summary>
        public static GameObject UserPanelPrefab { get; set; }

        [Header("交互（留空则按名字自动查找）")]
        [SerializeField] private Button closeButton;    // Close 按钮：关闭面板
        [SerializeField] private Button editorButton;   // Editor 按钮：激活昵称编辑

        [Header("显示（留空则按名字自动查找）")]
        [SerializeField] private TextMeshProUGUI idText;       // ID：显示玩家 ID
        [SerializeField] private TMP_InputField userNameInput; // UserName：昵称输入框
        [SerializeField] private TextMeshProUGUI levelText;    // UserLevel：庭院等级（纯文本显示）

        private UserProfileResponse _profile;
        private string _lastNickname;

        /// <summary>入口：实例化 UserPanel 预制体并打开（预制体缺失则告警返回 null）。</summary>
        public static UserPanel CreateOrInstantiate()
        {
            if (UserPanelPrefab == null)
            {
                Debug.LogWarning("[UserPanel] 未注入 UserPanel 预制体，跳过打开");
                return null;
            }
            if (Instance != null)
            {
                Instance.Open();
                return Instance;
            }
            var go = Instantiate(UserPanelPrefab);
            var panel = go.GetComponent<UserPanel>();
            if (panel == null)
            {
                Destroy(go);
                Debug.LogWarning("[UserPanel] 预制体缺少 UserPanel 组件");
                return null;
            }
            return panel;
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
            ResolveReferences();
        }

        private void Start()
        {
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (editorButton != null) editorButton.onClick.AddListener(OnEditClicked);
            if (userNameInput != null) userNameInput.onEndEdit.AddListener(OnNicknameEndEdit);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (closeButton != null) closeButton.onClick.RemoveAllListeners();
            if (editorButton != null) editorButton.onClick.RemoveAllListeners();
            if (userNameInput != null) userNameInput.onEndEdit.RemoveAllListeners();
        }

        public void Open()
        {
            gameObject.SetActive(true);
            LoadProfile();
        }

        public void Close()
        {
            gameObject.SetActive(false);
        }

        // ---------- 数据 ----------

        private void LoadProfile()
        {
            var net = NetManager.Instance;
            if (net == null) return;
            net.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp => ApplyProfile(resp.data),
                (code, msg) => Debug.LogWarning($"[UserPanel] profile 加载失败: {msg}"));
        }

        private void ApplyProfile(UserProfileResponse data)
        {
            if (data == null) return;
            _profile = data;

            // ID：玩家 ID
            if (idText != null) idText.text = data.userId.ToString();

            // UserLevel：庭院等级（= 玩家等级），纯文本显示，带 "LV." 前缀
            if (levelText != null) levelText.text = "LV." + data.level;

            // UserName：昵称（默认只读，点 Editor 后编辑）
            if (userNameInput != null)
            {
                userNameInput.readOnly = true;
                userNameInput.text = data.nickname ?? string.Empty;
                _lastNickname = userNameInput.text;
            }
        }

        // ---------- 交互 ----------

        /// <summary>点击 Editor：激活 UserName 输入框进入编辑态。</summary>
        private void OnEditClicked()
        {
            if (userNameInput == null) return;
            userNameInput.readOnly = false;
            userNameInput.ActivateInputField();
        }

        /// <summary>昵称输入结束（回车/失焦）：有变化则提交保存。</summary>
        private void OnNicknameEndEdit(string value)
        {
            if (userNameInput == null) return;
            var newName = (value ?? string.Empty).Trim();

            if (newName == _lastNickname)
            {
                // 未变化：恢复只读显示态即可
                userNameInput.readOnly = true;
                return;
            }
            if (string.IsNullOrEmpty(newName))
            {
                // 清空视为无效，回滚旧昵称
                userNameInput.text = _lastNickname;
                userNameInput.readOnly = true;
                return;
            }
            SaveNickname(newName);
        }

        private void SaveNickname(string nickname)
        {
            var net = NetManager.Instance;
            if (net == null) return;
            net.Put<UserProfileResponse>(ApiPaths.UserUpdateNickname,
                new UpdateNicknameRequest { nickname = nickname },
                resp =>
                {
                    _lastNickname = nickname;
                    if (userNameInput != null)
                    {
                        userNameInput.text = nickname;
                        userNameInput.readOnly = true;
                    }
                    ApplyProfile(resp.data);
                },
                (code, msg) =>
                {
                    // 失败回滚到旧昵称
                    if (userNameInput != null)
                    {
                        userNameInput.text = _lastNickname;
                        userNameInput.readOnly = true;
                    }
                    Debug.LogWarning($"[UserPanel] 昵称修改失败: {msg}");
                });
        }

        // ---------- 运行时按名字自动查找子节点（字段为空时兜底） ----------

        private void ResolveReferences()
        {
            if (closeButton == null) closeButton = FindChild<Button>("Close");
            if (editorButton == null) editorButton = FindChild<Button>("Editor");
            if (idText == null) idText = FindChild<TextMeshProUGUI>("ID");
            if (userNameInput == null) userNameInput = FindChild<TMP_InputField>("UserName");
            if (levelText == null) levelText = FindChild<TextMeshProUGUI>("UserLevel");
        }

        private T FindChild<T>(string name) where T : Component
        {
            var all = GetComponentsInChildren<T>(true);
            foreach (var c in all)
            {
                if (c.name == name) return c;
            }
            return null;
        }
    }
}
