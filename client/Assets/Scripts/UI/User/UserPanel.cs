using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

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
        [SerializeField] private Button editorNameButton; // EditorName 按钮（UserName 内）：激活昵称编辑并聚焦光标

        [Header("显示（留空则按名字自动查找）")]
        [SerializeField] private TextMeshProUGUI idText;       // ID：显示玩家 ID
        [SerializeField] private TMP_InputField userNameInput; // UserName：昵称输入框
        [SerializeField] private TextMeshProUGUI levelText;    // UserLevel：庭院等级（纯文本显示）

        [Header("头像")]
        [SerializeField] private Button avatarButton;          // Avatar 按钮：点击换头像
        [SerializeField] private Image avatarImage;            // Avatar/Image：头像显示图

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
            if (editorNameButton != null) editorNameButton.onClick.AddListener(OnEditClicked);
            if (userNameInput != null) userNameInput.onEndEdit.AddListener(OnNicknameEndEdit);
            if (avatarButton != null) avatarButton.onClick.AddListener(OnAvatarClicked);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (closeButton != null) closeButton.onClick.RemoveAllListeners();
            if (editorButton != null) editorButton.onClick.RemoveAllListeners();
            if (editorNameButton != null) editorNameButton.onClick.RemoveAllListeners();
            if (userNameInput != null) userNameInput.onEndEdit.RemoveAllListeners();
            if (avatarButton != null) avatarButton.onClick.RemoveAllListeners();
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

            // UserName：昵称（默认不可交互，点 Editor/EditorName 后可编辑）
            if (userNameInput != null)
            {
                userNameInput.text = data.nickname ?? string.Empty;
                _lastNickname = userNameInput.text;
            }
            SetEditEnabled(false);

            // 头像：有 URL 则加载显示
            if (!string.IsNullOrEmpty(data.avatarUrl))
            {
                AvatarLoader.LoadInto(avatarImage, AvatarLoader.ToAbsoluteUrl(data.avatarUrl), this);
            }
        }

        // ---------- 交互 ----------

        /// <summary>
        /// 控制输入框是否可交互。始终不切 readOnly（避免其与 ActivateInputField 竞态），
        /// 只通过 interactable 控制能否编辑，保证每次点击按钮都能稳定聚焦出光标。
        /// </summary>
        private void SetEditEnabled(bool enabled)
        {
            if (userNameInput == null) return;

            // 关闭交互必须延迟一帧：onEndEdit 触发时输入框正处于失焦/选中状态切换中，
            // 若此刻同步设 interactable=false 会打断 Selectable 状态机，
            // 触发 "Attempting to select while already selecting an object"。
            if (!enabled)
            {
                StartCoroutine(DisableInteractableNextFrame());
                return;
            }

            userNameInput.interactable = true;
        }

        private System.Collections.IEnumerator DisableInteractableNextFrame()
        {
            yield return null;
            if (userNameInput == null) yield break;
            if (EventSystem.current != null &&
                EventSystem.current.currentSelectedGameObject == userNameInput.gameObject)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
            userNameInput.interactable = false;
        }

        /// <summary>点击 Editor / EditorName：让 UserName 输入框可交互并获得焦点显示光标。</summary>
        private void OnEditClicked()
        {
            if (userNameInput == null) return;
            userNameInput.interactable = true;
            // 等一帧再聚焦，确保 interactable 与 EventSystem 状态就绪，光标稳定出现
            StartCoroutine(ActivateAndFocusNextFrame());
        }

        private System.Collections.IEnumerator ActivateAndFocusNextFrame()
        {
            yield return null;
            if (userNameInput == null) yield break;

            // 先清掉按钮的选中状态，避免 "Attempting to select while already selecting"
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            userNameInput.ActivateInputField();
            userNameInput.caretPosition = userNameInput.text.Length;
            userNameInput.MoveTextEnd(false);
        }

        /// <summary>昵称输入结束（回车/失焦）：有变化则提交保存。</summary>
        private void OnNicknameEndEdit(string value)
        {
            if (userNameInput == null) return;
            var newName = (value ?? string.Empty).Trim();

            if (newName == _lastNickname)
            {
                // 未变化：回到不可交互显示态
                SetEditEnabled(false);
                return;
            }
            if (string.IsNullOrEmpty(newName))
            {
                // 清空视为无效，回滚旧昵称
                userNameInput.text = _lastNickname;
                SetEditEnabled(false);
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
                    }
                    ApplyProfile(resp.data);
                    // 广播昵称更新，供 YardMainUI 等同步
                    EventBus.Publish(new ProfileUpdatedEvent
                    {
                        AvatarUrl = resp.data != null ? resp.data.avatarUrl : null,
                        Nickname = resp.data != null ? resp.data.nickname : nickname
                    });
                },
                (code, msg) =>
                {
                    // 失败回滚到旧昵称
                    if (userNameInput != null)
                    {
                        userNameInput.text = _lastNickname;
                    }
                    SetEditEnabled(false);
                    Debug.LogWarning($"[UserPanel] 昵称修改失败: {msg}");
                });
        }

        // ---------- 头像 ----------

        /// <summary>点击 Avatar 按钮：调系统相册选图（Android 用 NativeGallery），选后上传。</summary>
        private void OnAvatarClicked()
        {
#if UNITY_ANDROID && !UNITY_EDITOR
            // GetImageFromGallery 内部已自动处理相册权限（RequestPermissionAsync），无需手动请求
            NativeGallery.GetImageFromGallery((path) =>
            {
                if (string.IsNullOrEmpty(path))
                {
                    Debug.Log("[UserPanel] 取消选择头像");
                    return;
                }
                // LoadImageAtPath 返回 Texture2D，再编码为 PNG 字节上传
                var tex = NativeGallery.LoadImageAtPath(path, 512, false, false);
                if (tex == null)
                {
                    Debug.LogWarning("[UserPanel] 头像读取失败");
                    return;
                }
                byte[] bytes = tex.EncodeToPNG();
                UnityEngine.Object.Destroy(tex);
                if (bytes == null || bytes.Length == 0)
                {
                    Debug.LogWarning("[UserPanel] 头像编码失败");
                    return;
                }
                if (bytes.Length > 8 * 1024 * 1024)
                {
                    Debug.LogWarning("[UserPanel] 头像图片过大（>8MB）");
                    return;
                }
                UploadAvatar(bytes, ".png");
            }, "选择头像", "image/*");
#else
            // 编辑器/PC 兜底：提示需在 Android 真机测试
            Debug.LogWarning("[UserPanel] 头像选择仅支持 Android 真机（需 NativeGallery 插件）");
#endif
        }

        /// <summary>上传头像字节到服务端，成功后刷新显示。</summary>
        private void UploadAvatar(byte[] bytes, string extension)
        {
            var net = NetManager.Instance;
            if (net == null) return;
            var ext = (extension ?? ".png").ToLowerInvariant();
            if (ext is not (".png" or ".jpg" or ".jpeg")) ext = ".png";

            net.PutMultipart<UserProfileResponse>(ApiPaths.UserUpdateAvatar, "avatar",
                "avatar" + ext, bytes,
                resp =>
                {
                    // 头像内容已变但 URL 不变（/uploads/avatar/{userId}.png），
                    // 先失效该 URL 的图片缓存，强制重新下载新头像
                    if (resp.data != null && !string.IsNullOrEmpty(resp.data.avatarUrl))
                    {
                        AvatarLoader.Invalidate(resp.data.avatarUrl);
                    }
                    ApplyProfile(resp.data);
                    // 广播头像更新，供 YardMainUI 等同步
                    EventBus.Publish(new ProfileUpdatedEvent
                    {
                        AvatarUrl = resp.data != null ? resp.data.avatarUrl : null,
                        Nickname = resp.data != null ? resp.data.nickname : null
                    });
                },
                (code, msg) =>
                {
                    Debug.LogWarning($"[UserPanel] 头像上传失败: {msg}");
                });
        }

        // ---------- 运行时按名字自动查找子节点（字段为空时兜底） ----------

        private void ResolveReferences()
        {
            if (closeButton == null) closeButton = FindChild<Button>("Close");
            if (editorButton == null) editorButton = FindChild<Button>("Editor");
            if (editorNameButton == null) editorNameButton = FindChild<Button>("EditorName");
            if (idText == null) idText = FindChild<TextMeshProUGUI>("ID");
            if (userNameInput == null) userNameInput = FindChild<TMP_InputField>("UserName");
            if (levelText == null) levelText = FindChild<TextMeshProUGUI>("UserLevel");
            if (avatarButton == null) avatarButton = FindChild<Button>("Avatar");
            // Avatar 节点自身就带有 Image 组件（按钮那张图），换的就是它的 sprite
            if (avatarImage == null && avatarButton != null)
            {
                avatarImage = avatarButton.GetComponent<Image>();
            }
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
