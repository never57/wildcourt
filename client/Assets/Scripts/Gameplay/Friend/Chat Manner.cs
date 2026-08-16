using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;

public enum MessageSender
{
    Me,
    Friend
}

/// <summary>
/// 好友私聊 UI（对接服务端 FriendController / ChatController + ChatHub 实时推送）。
/// - 打开聊天：拉取好友列表 + 待处理申请
/// - 点击好友：拉取与该好友的历史消息
/// - 发送：POST /chat/send，成功后本地显示
/// - 接收：订阅 PrivateMessageEvent（ChatHub ReceivePrivateMessage 推送）
/// </summary>
public class ChatManner : MonoBehaviour
{
    public static ChatManner Instance { get; private set; }

    [Header("窗口控制")]
    public GameObject chatWindow;  //聊天窗口
    public Button closeButton;  //关闭

    [Header("好友列表")]
    public Transform friendListContent;  //好友按钮
    public GameObject friendButtonPrefab;  //好友按钮预制体

    [Header("消息列表")]
    public Transform messContent; // 存放消息
    public GameObject myMessagePrefab;  // 本人消息预制体
    public GameObject friendMessagePrefab;  // 好友消息预制体

    [Header("输入框")]
    public TMP_InputField inputField;  // 输入框
    public Button sendButton;  // 发送按钮

    [Header("时间标签")]
    public GameObject timeLabelPrefab;  //时间标签预制体
    private string lastMessageTime = "";  //上条消息时间

    [Header("头像")]
    public Sprite myAvatar;  // 本人头像
    public Sprite friendAvatar;  // 好友头像

    [Header("标题")]
    public TMP_Text titleText;  // 标题好友名字喵

    [Header("消息更新到最新")]
    public ScrollRect chatScrollRect;  // 滚动区域喵

    // ---- 会话状态 ----
    private long _myUserId;                 // 当前登录用户 ID
    private long _activeFriendId = -1;     // 当前正在聊天的好友 ID
    private readonly List<FriendItem> _friends = new List<FriendItem>();

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        // 订阅实时私聊推送
        EventBus.Subscribe<PrivateMessageEvent>(OnPrivateMessage);
    }

    private void Start()
    {
        //清空输入框消息
        inputField.text = "";

        //测试喵
        

        // 将窗口组件全部禁止
        if (chatWindow != null)
        {
            var cg = chatWindow.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = false;
                cg.alpha = 0;
                cg.interactable = false;
            }
        }

        // 绑定按钮事件（打开聊天改由 YardMainUI 的 friends 按钮触发，此处不再绑定 chatButton）
        if (closeButton != null) closeButton.onClick.AddListener(ChatClose);
        if (sendButton != null) sendButton.onClick.AddListener(SendMessage);

        // 拉取当前用户信息（拿到 userId 用于判断消息左右侧）
        LoadMyProfile();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        EventBus.Unsubscribe<PrivateMessageEvent>(OnPrivateMessage);
    }

    // ============ 窗口控制 ============

    /// <summary>
    /// 实例化或复用聊天 UI（chat.prefab，路径 Prefabs/UI/Chat/chat）。
    /// 与 MailPanel.CreateOrInstantiate 同一模式：首次调用 Resources.Load 实例化，之后复用单例。
    /// </summary>
    public static ChatManner CreateOrInstantiate()
    {
        if (Instance != null)
        {
            Instance.OpenChat();
            return Instance;
        }

        var prefab = Resources.Load<GameObject>("Prefabs/UI/Chat/chat");
        if (prefab == null)
        {
            Debug.LogWarning("[ChatManner] 未找到 chat.prefab（Prefabs/UI/Chat/chat）");
            return null;
        }

        var go = Instantiate(prefab);
        var manner = go.GetComponent<ChatManner>();
        if (manner == null)
        {
            Destroy(go);
            Debug.LogWarning("[ChatManner] chat.prefab 缺少 ChatManner 组件");
            return null;
        }
        return manner;
    }

    public void OpenChat()
    {
        if (chatWindow != null)
        {
            var cg = chatWindow.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = true;
                cg.alpha = 1;
                cg.interactable = true;
            }
        }

        // 打开时刷新好友列表
        LoadFriendList();
    }

    public void ChatClose()
    {
        if (chatWindow != null)
        {
            var cg = chatWindow.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = false;
                cg.alpha = 0;
                cg.interactable = false;
            }
        }
    }

    // ============ 服务端数据加载 ============

    private void LoadMyProfile()
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<UserProfileResponse>(ApiPaths.UserProfile,
            resp =>
            {
                if (resp != null && resp.data != null)
                {
                    _myUserId = resp.data.userId;
                }
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取用户信息失败：{msg}"));
    }

    private void LoadFriendList()
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<FriendListResponse>(ApiPaths.FriendList,
            resp =>
            {
                _friends.Clear();
                if (resp != null && resp.data != null && resp.data.friends != null)
                {
                    _friends.AddRange(resp.data.friends);
                }
                RebuildFriendList();
                if(_friends.Count > 0)
                {
                    OpenFriendChat(_friends[0].userId);  // 默认打开第一个好友聊天
                    ScrollToBottom();
                }
                else
                {
                    UpdateTitle("");
                    ClearMessages();
                }
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取好友列表失败：{msg}"));
    }

    private void RebuildFriendList()
    {
        if (friendListContent == null || friendButtonPrefab == null) return;

        // 清空旧按钮
        for (int i = friendListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(friendListContent.GetChild(i).gameObject);
        }

        foreach (var f in _friends)
        {
            var go = Instantiate(friendButtonPrefab, friendListContent);
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = string.IsNullOrEmpty(f.nickname) ? f.userId.ToString() : f.nickname;

            // 点击好友 → 拉历史消息
            long friendId = f.userId;
            var btn = go.GetComponent<Button>();
            if (btn == null) btn = go.GetComponentInChildren<Button>();
            if (btn != null)
            {
                btn.onClick.RemoveAllListeners();
                btn.onClick.AddListener(() => OpenFriendChat(friendId));
            }
        }
    }

    //更新当前聊天对象喵
    private void OpenFriendChat(long friendId)
    {
        _activeFriendId = friendId;
        //更新标题
        string friendName = GetFriendName(friendId);
        UpdateTitle(friendName);

        // 清空消息区
        ClearMessages();
        LoadHistory(friendId);
    }

    private void UpdateTitle(string name)
    {
        if(titleText != null)
        {
            titleText.text = name;
        }
    }

    private string GetFriendName(long friendId)
    {
        foreach(var i in _friends)
        {
            if(i.userId == friendId)
            {
                return string.IsNullOrEmpty(i.nickname) ? i.userId.ToString() : i.nickname;
            }
        }
        return friendId.ToString();
    }

    private void LoadHistory(long friendId)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<ChatHistoryResponse>(ApiPaths.ChatHistory(friendId),
            resp =>
            {
                if (resp != null && resp.data != null && resp.data.messages != null)
                {
                    foreach (var m in resp.data.messages)
                    {
                        AddMessage(m.content, m.fromUserId == _myUserId ? MessageSender.Me : MessageSender.Friend, m.avatarUrl);
                    }
                }
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取历史消息失败：{msg}"));
    }

    private void ClearMessages()
    {
        if (messContent == null) return;
        for (int i = messContent.childCount - 1; i >= 0; i--)
        {
            Destroy(messContent.GetChild(i).gameObject);
        }
        lastMessageTime = "";
    }

    // ============ 发送 / 接收 ============

    private void SendMessage()
    {
        if (inputField == null) return;
        string msg = inputField.text;
        if (string.IsNullOrEmpty(msg)) return;
        if (_activeFriendId <= 0)
        {
            Debug.LogWarning("[Chat] 未选择好友，无法发送");
            return;
        }
        if (NetManager.Instance == null) return;

        NetManager.Instance.Post<ChatMessageDto>(ApiPaths.ChatSend,
            new SendChatRequest { toUserId = _activeFriendId, content = msg },
            resp =>
            {
                // 发送成功：本地显示自己的消息
                if (resp != null && resp.data != null)
                {
                    AddMessage(resp.data.content, MessageSender.Me);
                    ScrollToBottom();
                }
            },
            (code, errMsg) => Debug.LogWarning($"[Chat] 发送失败：{errMsg}"));

        inputField.text = "";
    }

    private void ScrollToBottom()
    {
        if(chatScrollRect != null)
        {
            Canvas.ForceUpdateCanvases();
            chatScrollRect.verticalNormalizedPosition = 0f;
            Debug.Log("已到最下方喵");
        }
    }

    private void OnPrivateMessage(PrivateMessageEvent evt)
    {
        // 实时收到好友消息：仅当正在与该好友聊天时显示，否则忽略（好友列表可加未读角标，暂略）
        if (_activeFriendId > 0 && evt.FromUserId == _activeFriendId)
        {
            AddMessage(evt.Content, MessageSender.Friend);
        }
        else
        {
            Debug.Log($"[Chat] 收到 {evt.FromUserId} 的消息（未打开该会话）：{evt.Content}");
        }
    }

    // ============ UI 渲染（保留原实现） ============

    private void AddMessage(string text, MessageSender sender = MessageSender.Me,string avatarUrl = null)
    {
        // 检查是否需要显示时间
        string currentTime = System.DateTime.Now.ToString("HH:mm");
        if (ShouldShowTime())
        {
            GameObject timeObj = Instantiate(timeLabelPrefab, messContent);
            TMP_Text timeText = timeObj.GetComponentInChildren<TMP_Text>();
            if (timeText != null) timeText.text = currentTime;
            lastMessageTime = currentTime;
        }

        GameObject PrefabToUse = (sender == MessageSender.Me) ? myMessagePrefab : friendMessagePrefab;

        GameObject msgObj = Instantiate(PrefabToUse, messContent);
        TMP_Text msgText = msgObj.GetComponentInChildren<TMP_Text>();
        if (msgText != null) msgText.text = text;

        Canvas.ForceUpdateCanvases();
        float textWidth = msgText.preferredWidth;
        float textHeight = msgText.preferredHeight;
        var bg = msgObj.transform.Find("Background");
        if (bg != null)
        {
            RectTransform bgRect = bg.GetComponent<RectTransform>();
            float padding = 10f;
            bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + padding);
            bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + padding);
        }

        // 设置头像
        var avatarNode = msgObj.transform.Find("Avatar");
        if (avatarNode != null)
        {
            Image avatar = avatarNode.GetComponent<Image>();
            if(avatar != null)
            {
                if (!string.IsNullOrEmpty(avatarUrl))
                {
                    //从服务器中加载头像喵
                    string fullUrl = AvatarLoader.ToAbsluteUrl(avatarUrl);
                    AvatarLoader.LoadInto(avatar, fullUrl,this);
                }
                else
                {
                    //使用默认头像
                    avatar.sprite = (sender == MessageSender.Me) ? myAvatar : friendAvatar;
                }
            }
        }
        ScrollToBottom();
    }

    private bool ShouldShowTime()
    {
        string currentTime = System.DateTime.Now.ToString("HH:mm");
        if (string.IsNullOrEmpty(lastMessageTime)) return true;

        System.DateTime last = System.DateTime.ParseExact(lastMessageTime, "HH:mm", null);
        System.DateTime now = System.DateTime.Now;
        System.TimeSpan diff = now - last;

        return diff.TotalMinutes >= 3;
    }
}
