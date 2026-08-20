using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.UI.User;
using WildCourt.Utils;

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
    public GameObject friendSystemRoot;  //整个 FriendSystem 根节点（close 控制其显示/隐藏）
    public GameObject chatWindow;  //聊天窗口（Chat Window）
    public Button closeButton;  //关闭

    [Header("搜索/申请窗口")]
    public GameObject searchWindow;      //搜索窗口（SearchArea）
    public Button chatButton;            //chat 按钮：显示聊天区
    public Button searchButton;          //search 按钮：显示搜索/申请区
    public Transform searchScrollContent; //Search Scroll View 的 Content
    public GameObject applicationPrefab;  //好友申请条目预制体（application.prefab）
    public TMP_Text tipText;              //Tip：无申请时显示提示

    [Header("搜索玩家")]
    public TMP_InputField searchInput;    //搜索输入框（InputField (TMP)）
    public Button searchConfirmButton;    //执行搜索的按钮（searchWindow 里的 search 按钮）
    public GameObject playerInfoPrefab;   //搜索结果条目预制体（playerinfromation.prefab）
    public Transform searchResultContent; //搜索结果容器（FriendsWindow/search/Search Content）
    public TMP_Text searchTipText;        //搜索结果的 Tip：无搜索结果时显示提示（与好友申请的 tipText 独立）

    [Header("玩家信息")]
    public UnityEngine.UI.Image labelAvatarImage; //Label 子节点下的 Avatar（显示自己头像）
    public TMP_Text labelNameText;                //Label 子节点下的 Text (TMP)（显示自己昵称）

    [Header("好友列表")]
    public Transform friendListContent;  //好友按钮
    public GameObject friendButtonPrefab;  //好友按钮预制体
    public TMP_InputField friendSearchInput;  //好友搜索输入框（Chat Window 顶部，按昵称/ID 过滤好友）
    public Button friendSearchButton;         //好友搜索按钮（search）
    public GameObject friendSearchStatus;     //好友搜索无结果提示（SearchStatus，搜不到时显示）

    [Header("消息列表")]
    public Transform messContent; // 存放消息
    public GameObject myMessagePrefab;  // 本人消息预制体
    public GameObject friendMessagePrefab;  // 好友消息预制体

    [Header("菜单动画")]
    public Button menuButton;  // 标题栏右侧 Menu 按钮（点击播放 FriendsInformation 动画）
    public FriendInfoAnimController friendInfoController;  // FriendsInformation 的动画控制器

    [Header("输入框")]
    public TMP_InputField inputField;  // 输入框
    public Button sendButton;  // 发送按钮

    [Header("时间标签")]
    public GameObject timeLabelPrefab;  //时间标签预制体
    private System.DateTime _lastMessageTime = System.DateTime.MinValue;  //上条消息时间（含日期，避免跨天判断错误）

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

    // ---- 未读角标状态 ----
    private readonly Dictionary<long, int> _unreadCounts = new Dictionary<long, int>();   // friendId → 未读消息数
    private readonly Dictionary<long, FriendUnreadBadge> _friendBadges = new Dictionary<long, FriendUnreadBadge>(); // friendId → 角标（红点 + 数字文本）

    // ---- 好友列表过滤状态 ----
    private readonly Dictionary<long, GameObject> _friendRowNodes = new Dictionary<long, GameObject>(); // friendId → Friend Button 实例（搜索过滤用）

    /// <summary>好友条目的未读角标：Status 红点 + 其下 UnreadCount 数字文本。</summary>
    private class FriendUnreadBadge
    {
        public GameObject status;   // Status 红点节点
        public TMP_Text unreadText; // Status/UnreadCount 数字文本
    }

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
        // 订阅玩家资料更新（改头像/昵称后广播），实时同步 Label 的头像 + 昵称
        EventBus.Subscribe<ProfileUpdatedEvent>(OnProfileUpdated);
    }

    private void Start()
    {
        //清空输入框消息
        inputField.text = "";

        // 窗口显示/隐藏统一由 friendSystemRoot.SetActive 控制（OpenChat / ChatClose），
        // 不再用 CanvasGroup 隐藏，避免 Start 在首次 OpenChat 之后又把窗口变透明。

        // 绑定按钮事件
        if (closeButton != null) closeButton.onClick.AddListener(ChatClose);
        if (sendButton != null) sendButton.onClick.AddListener(SendMessage);
        if (chatButton != null) chatButton.onClick.AddListener(ShowChatArea);
        if (searchButton != null) searchButton.onClick.AddListener(ShowSearchArea);
        if (searchConfirmButton != null) searchConfirmButton.onClick.AddListener(OnSearchClick);
        if (friendSearchButton != null) friendSearchButton.onClick.AddListener(OnFriendSearchClick);
        if (menuButton != null) menuButton.onClick.AddListener(OnMenuClick);

        // 拉取当前用户信息（拿到 userId 用于判断消息左右侧）
        LoadMyProfile();
    }

    private void OnDestroy()
    {
        if (Instance == this) Instance = null;
        EventBus.Unsubscribe<PrivateMessageEvent>(OnPrivateMessage);
        EventBus.Unsubscribe<ProfileUpdatedEvent>(OnProfileUpdated);
    }

    // ============ 窗口控制 ============

    /// <summary>
    /// 实例化或复用聊天 UI（FriendSystem.prefab，路径 Prefabs/UI/Friends/FriendSystem）。
    /// 与 MailPanel.CreateOrInstantiate 同一模式：首次调用 Resources.Load 实例化，之后复用单例。
    /// </summary>
    public static ChatManner CreateOrInstantiate()
    {
        if (Instance != null)
        {
            Instance.OpenChat();
            return Instance;
        }

        var prefab = Resources.Load<GameObject>("Prefabs/UI/Friends/FriendSystem");
        if (prefab == null)
        {
            Debug.LogWarning("[ChatManner] 未找到 FriendSystem.prefab（Prefabs/UI/Friends/FriendSystem）");
            return null;
        }

        var go = Instantiate(prefab);
        var manner = go.GetComponent<ChatManner>();
        if (manner == null)
        {
            Destroy(go);
            Debug.LogWarning("[ChatManner] FriendSystem.prefab 缺少 ChatManner 组件");
            return null;
        }
        // 首次实例化后主动打开（显示窗口 + 加载好友列表），与复用分支行为一致
        manner.OpenChat();
        return manner;
    }

    public void OpenChat()
    {
        // 打开整个 FriendSystem
        if (friendSystemRoot != null)
        {
            friendSystemRoot.SetActive(true);
        }
        else if (chatWindow != null)
        {
            var cg = chatWindow.GetComponent<CanvasGroup>();
            if (cg != null)
            {
                cg.blocksRaycasts = true;
                cg.alpha = 1;
                cg.interactable = true;
            }
        }

        // 默认显示聊天区（ShowChatArea 内部会清空搜索栏）
        ShowChatArea();

        // 每次打开都重新拉取 profile 刷新 Label 头像+昵称：
        // ChatManner 是单例复用，LoadMyProfile 只在首次实例化（Start）执行，
        // 若不在此刷新，重开好友系统会用旧数据，导致改头像后不更新（重启游戏才更新）。
        RefreshLabelPlayerInfo();

        // 打开时刷新好友列表
        LoadFriendList();
    }

    public void ChatClose()
    {
        // 关闭时清空好友搜索（清空输入框 + 恢复显示全部好友）
        ClearFriendSearch();

        // 关闭整个 FriendSystem
        if (friendSystemRoot != null)
        {
            friendSystemRoot.SetActive(false);
        }
        else if (chatWindow != null)
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

    // ============ 聊天区 / 搜索区切换 ============

    /// <summary>显示聊天区（显示 Chat Window，隐藏搜索区），并清空搜索栏。</summary>
    public void ShowChatArea()
    {
        if (chatWindow != null) chatWindow.SetActive(true);
        if (searchWindow != null) searchWindow.SetActive(false);
        // 从搜索切回聊天时清空搜索栏（输入框 + 结果列表 + 提示），不保留上次搜索内容
        ClearSearchArea();
    }

    /// <summary>清空搜索栏：输入框文字、搜索结果列表、搜索提示。</summary>
    private void ClearSearchArea()
    {
        if (searchInput != null) searchInput.text = "";
        ClearSearchResults();
        HideSearchTip();
    }

    /// <summary>显示搜索/申请区（显示搜索区，隐藏聊天区），并刷新好友申请列表。</summary>
    public void ShowSearchArea()
    {
        // 点击 Label 里的 search 切换到搜索界面时，清空好友列表搜索状态并恢复默认显示
        ClearFriendSearch();

        if (searchWindow != null) searchWindow.SetActive(true);
        if (chatWindow != null) chatWindow.SetActive(false);
        LoadFriendRequests();
    }

    // ============ 好友申请 ============

    /// <summary>拉取待处理的好友申请列表并渲染到 Search Scroll View。</summary>
    private void LoadFriendRequests()
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<FriendRequestListResponse>(ApiPaths.FriendRequests,
            resp =>
            {
                var list = (resp != null && resp.data != null && resp.data.requests != null)
                    ? resp.data.requests
                    : new FriendRequestItem[0];

                RebuildRequestList(list);
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取好友申请列表失败：{msg}"));
    }

    private void RebuildRequestList(FriendRequestItem[] requests)
    {
        if (searchScrollContent == null) return;

        // 清空旧条目
        for (int i = searchScrollContent.childCount - 1; i >= 0; i--)
        {
            Destroy(searchScrollContent.GetChild(i).gameObject);
        }

        // 无申请：显示 Tip，隐藏列表
        bool hasRequests = requests != null && requests.Length > 0;
        if (tipText != null)
        {
            tipText.gameObject.SetActive(!hasRequests);
        }

        if (!hasRequests) return;

        if (applicationPrefab == null)
        {
            Debug.LogWarning("[Chat] 未绑定 applicationPrefab（application.prefab）");
            return;
        }

        foreach (var req in requests)
        {
            var go = Instantiate(applicationPrefab, searchScrollContent);
            var item = go.GetComponent<WildCourt.UI.Friends.ApplicationItem>();
            if (item == null)
            {
                item = go.AddComponent<WildCourt.UI.Friends.ApplicationItem>();
            }
            item.Bind(req, () =>
            {
                // 同意/拒绝后刷新列表 + 好友列表
                LoadFriendRequests();
                LoadFriendList();
            });
        }
    }

    // ============ 搜索玩家 ============

    /// <summary>点击搜索按钮：读取输入框，搜索玩家并渲染结果。</summary>
    public void OnSearchClick()
    {
        if (searchInput == null)
        {
            Debug.LogWarning("[Chat] 未绑定 searchInput（InputField (TMP)）");
            return;
        }
        SearchPlayer(searchInput.text);
    }

    /// <summary>按昵称/UserId 搜索玩家，结果用 playerinfromation 渲染到 searchScrollContent。</summary>
    /// <summary>显示搜索 Tip（需同时启用组件 + 激活 GameObject，因为 Tip 的 TMP_Text 组件默认 m_Enabled: 0）。</summary>
    private void ShowSearchTip(string text)
    {
        if (searchTipText == null) return;
        searchTipText.text = text;
        searchTipText.enabled = true;                 // 启用 TMP_Text 组件（可能默认禁用）
        searchTipText.gameObject.SetActive(true);     // 激活 GameObject
    }

    private void HideSearchTip()
    {
        if (searchTipText == null) return;
        searchTipText.gameObject.SetActive(false);
    }

    private void SearchPlayer(string keyword)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            ClearSearchResults();
            ShowSearchTip("请输入玩家 ID");
            return;
        }

        if (NetManager.Instance == null) return;

        string kw = keyword.Trim();

        // 只允许按玩家 ID 精确搜索（纯数字），不再支持昵称模糊搜索
        if (!long.TryParse(kw, out _))
        {
            ClearSearchResults();
            ShowSearchTip("请输入玩家 ID（纯数字）");
            return;
        }

        // 搜索自己：明确提示不能添加自己为好友
        // 用 ChatManner 自己的 _myUserId 作为主要判断（LoadMyProfile 已赋值），AvatarManager 作兜底
        long myId = _myUserId;
        if (myId <= 0 && AvatarManager.Instance != null) myId = AvatarManager.Instance.MyUserId;
        if (myId > 0 && long.TryParse(kw, out var selfUid) && selfUid == myId)
        {
            ClearSearchResults();
            ShowSearchTip("不能添加自己为好友");
            return;
        }

        string path = ApiPaths.UserSearch + "?keyword=" + UnityEngine.Networking.UnityWebRequest.EscapeURL(kw);
        NetManager.Instance.Get<System.Collections.Generic.List<PlayerSearchDto>>(path,
            resp =>
            {
                RebuildSearchResults(resp != null ? resp.data : null);
            },
            (code, msg) => Debug.LogWarning($"[Chat] 搜索玩家失败：{msg}"));
    }

    private void ClearSearchResults()
    {
        if (searchResultContent == null) return;
        for (int i = searchResultContent.childCount - 1; i >= 0; i--)
        {
            Destroy(searchResultContent.GetChild(i).gameObject);
        }
    }

    private void RebuildSearchResults(System.Collections.Generic.List<PlayerSearchDto> results)
    {
        ClearSearchResults();

        bool hasResults = results != null && results.Count > 0;
        if (hasResults)
        {
            HideSearchTip();
        }
        else
        {
            ShowSearchTip("没有找到符合条件的玩家");
        }
        if (!hasResults) return;

        if (playerInfoPrefab == null)
        {
            Debug.LogWarning("[Chat] 未绑定 playerInfoPrefab（playerinfromation.prefab）");
            return;
        }

        foreach (var p in results)
        {
            var go = Instantiate(playerInfoPrefab, searchResultContent);
            // 先隐藏再绑定，避免「默认黄色按钮 → 变灰」/「默认头像 → 真实头像」的可见闪烁
            go.SetActive(false);
            BindPlayerInfo(go, p);
            go.SetActive(true);
        }
    }

    /// <summary>绑定 playerinfromation 预制体：Avatar 头像、Name 名字、describe 个性签名。</summary>
    private void BindPlayerInfo(GameObject go, PlayerSearchDto p)
    {
        if (go == null || p == null) return;

        // Name 名字（在 Avatar_background 子节点下）
        var nameText = FindText(go, "Name");
        if (nameText != null)
        {
            nameText.text = string.IsNullOrEmpty(p.nickname) ? p.userId.ToString() : p.nickname;
        }

        // describe 个性签名（未设置时显示默认文案）
        var describeText = FindText(go, "describe");
        if (describeText != null)
        {
            describeText.text = string.IsNullOrEmpty(p.signature) ? "该玩家很神秘，什么也没有留下~" : p.signature;
        }

        // Avatar 头像
        var avatarImage = FindImage(go, "Avatar");
        if (avatarImage != null && !string.IsNullOrEmpty(p.avatarUrl))
        {
            AvatarLoader.LoadInto(avatarImage, p.avatarUrl, this);
        }

        // application 按钮：若已是好友则禁用（变灰）并修改文案；否则绑定点击发起好友申请
        var appNode = FindNode(go, "application");
        if (appNode != null)
        {
            var appButton = appNode.GetComponent<Button>();
            var appText = appNode.GetComponentInChildren<TMP_Text>();
            // 优先用服务端返回的 isFriend 标记，客户端 _friends 列表作兜底
            bool isFriend = p.isFriend || IsFriend(p.userId);
            if (appButton != null)
            {
                appButton.interactable = !isFriend;
                // 绑定点击：发起好友申请
                appButton.onClick.RemoveAllListeners();
                long targetId = p.userId;
                appButton.onClick.AddListener(() => SendFriendApply(targetId, appButton, appText));
            }
            if (appText != null)
            {
                appText.text = isFriend ? "对方已经是你好友" : "申请";
            }
        }
    }

    /// <summary>发起好友申请；成功后禁用按钮并修改文案为「已发送申请」。</summary>
    private void SendFriendApply(long targetUserId, Button button, TMP_Text text)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Post<object>(ApiPaths.FriendApply,
            new FriendApplyRequest { userId = targetUserId },
            resp =>
            {
                if (button != null) button.interactable = false;   // 变灰
                if (text != null) text.text = "已发送申请";
            },
            (code, msg) => Debug.LogWarning($"[Chat] 发送好友申请失败：{msg}"));
    }

    /// <summary>判断指定 userId 是否已是好友（基于客户端 _friends 好友列表）。</summary>
    private bool IsFriend(long userId)
    {
        if (userId <= 0) return false;
        foreach (var f in _friends)
        {
            if (f.userId == userId) return true;
        }
        return false;
    }

    // ============ 递归查找工具（节点可能嵌套在 Avatar_background 等中间层下） ============

    /// <summary>递归查找指定名字的节点。</summary>
    private Transform FindNode(GameObject go, string nodeName)
    {
        if (go == null) return null;
        return FindInChildren(go.transform, nodeName);
    }

    private Transform FindInChildren(Transform t, string nodeName)
    {
        foreach (Transform child in t)
        {
            if (child.name == nodeName) return child;
            var found = FindInChildren(child, nodeName);
            if (found != null) return found;
        }
        return null;
    }

    /// <summary>递归查找指定名字节点下的 TMP_Text。</summary>
    private TMP_Text FindText(GameObject go, string nodeName)
    {
        var node = FindNode(go, nodeName);
        return node != null ? node.GetComponent<TMP_Text>() : null;
    }

    /// <summary>递归查找指定名字节点下的 Image。</summary>
    private UnityEngine.UI.Image FindImage(GameObject go, string nodeName)
    {
        var node = FindNode(go, nodeName);
        return node != null ? node.GetComponent<UnityEngine.UI.Image>() : null;
    }

    // ============ 服务端数据加载 ============

    private void LoadMyProfile()
    {
        // 当前用户 ID 统一从全局 AvatarManager 缓存读取（登录后已拉取 profile）
        _myUserId = AvatarManager.Instance != null ? AvatarManager.Instance.GetMyUserId() : -1;
        if (_myUserId <= 0)
        {
            Debug.LogWarning("[Chat] 尚未拿到当前用户 ID，请确认登录后 AvatarManager 已初始化");
        }

        // 同步 Label 子节点的玩家信息（头像 + 昵称）
        // 头像：若 AvatarManager 缓存已有则立即设置（加速显示，未命中靠 profile 补）
        if (labelAvatarImage != null)
        {
            string myAvatar = AvatarManager.Instance != null ? AvatarManager.Instance.MyAvatarUrl : null;
            if (!string.IsNullOrEmpty(myAvatar))
            {
                AvatarLoader.LoadInto(labelAvatarImage, myAvatar, this);
            }
        }
        // 昵称无本地缓存，无论头像缓存状态都必须拉取 profile 才能同步到 Label/Name。
        // 原实现只在头像缓存为空时才拉 profile，导致头像有缓存时昵称一直不同步。
        RefreshLabelPlayerInfo();
    }

    /// <summary>
    /// 玩家资料更新（改头像/昵称后广播）：实时同步 Label 子节点的头像 + 昵称，
    /// 避免必须重启游戏才能看到最新资料。头像/昵称直接用事件携带的值，无需再拉 profile。
    /// </summary>
    private void OnProfileUpdated(ProfileUpdatedEvent evt)
    {
        // 主动拉取 profile 并 force 刷新头像 + 昵称，确保拿到服务端最新数据，
        // 而非仅依赖事件携带的 URL（可能为空/旧值）。force 保证换头像后即使 URL 不变也强制重下。
        RefreshLabelPlayerInfo();
    }

    /// <summary>
    /// 拉取自己 profile，把头像 + 昵称更新到 Label 子节点（Label/Avatar、Label/Name）。
    /// </summary>
    private void RefreshLabelPlayerInfo()
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<UserProfileResponse>(ApiPaths.UserProfile,
            resp =>
            {
                if (resp == null || resp.data == null) return;
                var data = resp.data;

                if (_myUserId <= 0) _myUserId = data.userId;

                if (labelNameText != null)
                {
                    labelNameText.text = string.IsNullOrEmpty(data.nickname) ? data.userId.ToString() : data.nickname;
                }

                if (labelAvatarImage != null && !string.IsNullOrEmpty(data.avatarUrl))
                {
                    // 服务端头像 URL 带版本，换头像后 URL 变化，LoadInto 自动检测差异下载新头像
                    AvatarLoader.LoadInto(labelAvatarImage, data.avatarUrl, this);
                }
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取玩家资料失败：{msg}"));
    }

    private void LoadFriendList()
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<FriendListResponse>(ApiPaths.FriendList,
            resp =>
            {
                _friends.Clear();
                // 重新加载好友列表时，未读数以服务端统计为准（覆盖实时累计），随后重建列表点亮角标
                _unreadCounts.Clear();
                if (resp != null && resp.data != null && resp.data.friends != null)
                {
                    _friends.AddRange(resp.data.friends);
                    foreach (var f in resp.data.friends)
                    {
                        // 把好友头像喂入全局缓存，供消息头像与其它模块复用
                        if (AvatarManager.Instance != null)
                        {
                            AvatarManager.Instance.SetFriendAvatar(f.userId, f.avatarUrl);
                        }
                        // 记录服务端统计的未读消息数（用于 BindUnreadBadge 渲染数字角标）
                        if (f.unreadCount > 0)
                        {
                            _unreadCounts[f.userId] = f.unreadCount;
                        }
                    }
                }
                // 预热头像（命中缓存立即返回，不阻塞），列表立即渲染保持秒开；
                // 头像由 AvatarLoader 异步下载后平滑淡入，既快又无「默认图→真实图」硬跳变。
                PrefetchAvatars();
                OnAvatarsReady();
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取好友列表失败：{msg}"));
    }

    /// <summary>按是否有未读消息对好友列表排序：有未读的优先排在前面，其余保持原顺序。</summary>
    private void SortFriendsByUnread()
    {
        if (_friends == null || _friends.Count < 2) return;
        _friends.Sort((a, b) =>
        {
            bool aUnread = _unreadCounts.ContainsKey(a.userId);
            bool bUnread = _unreadCounts.ContainsKey(b.userId);
            if (aUnread == bUnread) return 0;   // 相同状态保持原顺序（稳定排序）
            return aUnread ? -1 : 1;            // 有未读的排前面
        });
    }

    private void RebuildFriendList()
    {
        if (friendListContent == null || friendButtonPrefab == null) return;

        // 清空旧按钮（同时清空角标/行节点映射，重建后重新填充）
        _friendBadges.Clear();
        _friendRowNodes.Clear();
        for (int i = friendListContent.childCount - 1; i >= 0; i--)
        {
            Destroy(friendListContent.GetChild(i).gameObject);
        }

        // 有未读消息的好友优先排在最上面
        SortFriendsByUnread();

        foreach (var f in _friends)
        {
            var go = Instantiate(friendButtonPrefab, friendListContent);
            _friendRowNodes[f.userId] = go;   // 记录 friendId → 按钮实例，供搜索过滤显示/隐藏
            var text = go.GetComponentInChildren<TMP_Text>();
            if (text != null) text.text = string.IsNullOrEmpty(f.nickname) ? f.userId.ToString() : f.nickname;

            // 未读角标：记录 Status 节点映射并按当前未读状态显示/隐藏
            BindUnreadBadge(go, f);

            // 好友头像：按玩家 ID 从 AvatarManager 缓存取（LoadFriendList 已喂入缓存），再加载显示。
            // 头像 URL 现已带版本号（/uploads/avatar/{userId}-{ts}.png），好友换头像 URL 会变化，
            // LoadInto 按 URL 判缓存自然重下，无需 force，也避免每次打开都重新下载。
            BindFriendAvatar(go, f.userId, f.avatarUrl);

            // 点击好友 → 拉历史消息（只允许点击 Friend Button 子节点 ChatButton 才进入聊天）
            long friendId = f.userId;
            var chatBtnNode = go.transform.Find("ChatButton");
            if (chatBtnNode == null) chatBtnNode = FindNode(go, "ChatButton");
            var chatBtn = chatBtnNode != null ? chatBtnNode.GetComponent<Button>() : null;
            if (chatBtn == null && chatBtnNode != null)
            {
                chatBtn = chatBtnNode.gameObject.AddComponent<Button>();
            }
            if (chatBtn != null)
            {
                chatBtn.onClick.RemoveAllListeners();
                chatBtn.onClick.AddListener(() => OpenFriendChat(friendId));
            }
            // 禁用 Friend Button 根节点自身的 Button，避免点击整块（头像/文字区域）就进入聊天
            var rootBtn = go.GetComponent<Button>();
            if (rootBtn != null && rootBtn != chatBtn)
            {
                rootBtn.onClick.RemoveAllListeners();
                rootBtn.enabled = false;
            }
        }
    }

    /// <summary>
    /// 给好友按钮实例加载头像：优先用 FriendItem.avatarUrl，其次从 AvatarManager 缓存按 userId 查。
    /// <param name="force">强制刷新：头像 URL 固定但内容可能更新（好友换头像），
    /// 传 true 会失效缓存并重新下载，确保显示最新头像。</param>
    /// </summary>
    private void BindFriendAvatar(GameObject go, long userId, string avatarUrl, bool force = false)
    {
        if (go == null) return;
        // Avatar 节点（Friend Button Prefab 根节点下）
        var avatarNode = go.transform.Find("Avatar");
        if (avatarNode == null) return;
        var avatarImage = avatarNode.GetComponent<UnityEngine.UI.Image>();
        if (avatarImage == null) return;

        string url = avatarUrl;
        if (string.IsNullOrEmpty(url) && AvatarManager.Instance != null)
        {
            url = AvatarManager.Instance.GetFriendAvatarUrl(userId);
        }
        if (!string.IsNullOrEmpty(url))
        {
            AvatarLoader.LoadInto(avatarImage, url, this, force);
        }
    }

    // ============ 未读角标（Friend Button 的 Status 节点） ============

    /// <summary>渲染好友按钮时，记录 Status 红点 + UnreadCount 文本映射，并按该好友当前未读数显示/隐藏。</summary>
    private void BindUnreadBadge(GameObject go, FriendItem f)
    {
        var statusNode = go.transform.Find("Status");
        if (statusNode == null) statusNode = FindNode(go, "Status");
        if (statusNode == null) return;

        var badge = new FriendUnreadBadge
        {
            status = statusNode.gameObject,
            unreadText = FindUnreadCountText(statusNode),
        };
        _friendBadges[f.userId] = badge;

        _unreadCounts.TryGetValue(f.userId, out var count);
        ApplyBadge(badge, count);
    }

    /// <summary>在 Status 节点下查找 UnreadCount 数字文本组件。</summary>
    private TMP_Text FindUnreadCountText(Transform statusNode)
    {
        var t = statusNode.Find("UnreadCount");
        if (t == null) t = FindNode(statusNode.gameObject, "UnreadCount");
        return t != null ? t.GetComponent<TMP_Text>() : null;
    }

    /// <summary>按未读数应用角标：有未读则显示 Status 红点并填数字（≥99 显示 99+），无未读则隐藏。</summary>
    private void ApplyBadge(FriendUnreadBadge badge, int count)
    {
        if (badge == null) return;
        bool has = count > 0;
        if (badge.status != null) badge.status.SetActive(has);
        if (badge.unreadText != null)
        {
            badge.unreadText.text = has ? FormatUnreadCount(count) : string.Empty;
        }
    }

    /// <summary>未读数格式化：低于 99 显示数字，大于等于 99 显示 99+。</summary>
    private static string FormatUnreadCount(int count)
    {
        return count >= 99 ? "99+" : count.ToString();
    }

    /// <summary>设置好友未读数（服务端拉取/初始化时用），并刷新角标。</summary>
    private void SetUnreadCount(long friendId, int count)
    {
        if (count > 0) _unreadCounts[friendId] = count;
        else _unreadCounts.Remove(friendId);
        if (_friendBadges.TryGetValue(friendId, out var badge))
        {
            ApplyBadge(badge, count);
        }
    }

    /// <summary>好友新增一条未读消息（实时消息用），未读数 +1 并刷新角标。</summary>
    private void AddUnread(long friendId)
    {
        _unreadCounts.TryGetValue(friendId, out var cur);
        SetUnreadCount(friendId, cur + 1);
    }

    /// <summary>清除好友未读状态，隐藏其 Friend Button 的 Status 角标。</summary>
    private void ClearFriendUnread(long friendId)
    {
        _unreadCounts.Remove(friendId);
        if (_friendBadges.TryGetValue(friendId, out var badge))
        {
            ApplyBadge(badge, 0);
        }
    }

    // ============ 好友搜索过滤（Chat Window 顶部的搜索框） ============

    /// <summary>点击 search 按钮：按昵称/ID 过滤好友列表，匹配的好友显示，不匹配的隐藏。</summary>
    private void OnFriendSearchClick()
    {
        var keyword = friendSearchInput != null ? friendSearchInput.text.Trim() : string.Empty;
        ApplyFriendFilter(keyword);
    }

    /// <summary>点击 Menu 按钮：播放 FriendsInformation 的动画。</summary>
    private void OnMenuClick()
    {
        if (friendInfoController != null)
        {
            friendInfoController.PlayAnimation();
        }
    }

    /// <summary>按关键字过滤好友列表：昵称或 ID 含关键字则显示，否则隐藏。空关键字显示全部。搜不到时显示 SearchStatus 提示。</summary>
    private void ApplyFriendFilter(string keyword)
    {
        bool hasFilter = !string.IsNullOrEmpty(keyword);
        int matchCount = 0;

        foreach (var f in _friends)
        {
            if (!_friendRowNodes.TryGetValue(f.userId, out var row) || row == null) continue;

            bool match;
            if (!hasFilter)
            {
                match = true;   // 空关键字显示全部
            }
            else
            {
                var name = string.IsNullOrEmpty(f.nickname) ? f.userId.ToString() : f.nickname;
                match = name.IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0
                        || f.userId.ToString().IndexOf(keyword, System.StringComparison.OrdinalIgnoreCase) >= 0;
            }
            row.SetActive(match);
            if (match) matchCount++;
        }

        // 搜索无匹配结果：显示 SearchStatus 提示；空搜索或存在匹配则隐藏
        if (friendSearchStatus != null)
        {
            friendSearchStatus.SetActive(hasFilter && matchCount == 0);
        }
    }

    /// <summary>清空好友搜索：清空输入框并恢复显示全部好友。</summary>
    private void ClearFriendSearch()
    {
        if (friendSearchInput != null) friendSearchInput.text = string.Empty;
        ApplyFriendFilter(string.Empty);
    }

    //更新当前聊天对象喵
    private void OpenFriendChat(long friendId)
    {
        _activeFriendId = friendId;
        // 打开聊天即视为已读，隐藏该好友的未读角标
        ClearFriendUnread(friendId);
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

    /// <summary>
    /// 预热所有头像（自己的 + 好友的）到 AvatarLoader 缓存。
    /// 命中缓存/下载中会直接跳过，不阻塞列表渲染；未命中的异步下载，完成后由 AvatarLoader 平滑淡入。
    /// </summary>
    private void PrefetchAvatars()
    {
        // 自己的头像
        if (AvatarManager.Instance != null)
        {
            var myUrl = AvatarManager.Instance.GetMyAvatarUrl();
            if (!string.IsNullOrEmpty(myUrl))
            {
                AvatarLoader.Prefetch(myUrl, this);
            }
        }

        // 所有好友头像
        foreach (var f in _friends)
        {
            if (!string.IsNullOrEmpty(f.avatarUrl))
            {
                AvatarLoader.Prefetch(f.avatarUrl, this);
            }
        }
    }

    /// <summary>渲染好友列表并默认打开第一个好友的聊天。</summary>
    private void OnAvatarsReady()
    {
        RebuildFriendList();
        if (_friends.Count > 0)
        {
            OpenFriendChat(_friends[0].userId);  // 默认打开第一个好友聊天
            ScrollToBottom();
        }
        else
        {
            UpdateTitle("");
            ClearMessages();
        }
    }

    private void LoadHistory(long friendId)
    {
        if (NetManager.Instance == null) return;
        NetManager.Instance.Get<ChatHistoryResponse>(ApiPaths.ChatHistory(friendId),
            resp =>
            {
                // 渲染消息前先把「自己 + 好友」头像同步载入内存缓存，
                // 这样 AddMessage 渲染时命中缓存秒回，避免「默认图→真实图」闪烁。
                PreloadChatAvatars(friendId);
                if (resp != null && resp.data != null && resp.data.messages != null)
                {
                    foreach (var m in resp.data.messages)
                    {
                        AddMessage(m.content, m.fromUserId == _myUserId ? MessageSender.Me : MessageSender.Friend, m.avatarUrl, m.fromUserId, m.createdAt);
                    }
                }
            },
            (code, msg) => Debug.LogWarning($"[Chat] 拉取历史消息失败：{msg}"));
    }

    /// <summary>
    /// 渲染聊天消息前，同步把「自己 + 好友」头像载入内存缓存（磁盘有则秒回）。
    /// 这样 AddMessage 渲染消息头像时能同步命中，避免「默认图→真实图」的异步闪烁。
    /// </summary>
    private void PreloadChatAvatars(long friendId)
    {
        // 自己的头像
        if (AvatarManager.Instance != null)
        {
            var myUrl = AvatarManager.Instance.GetMyAvatarUrl();
            if (!string.IsNullOrEmpty(myUrl)) AvatarLoader.PreloadIntoMemory(myUrl);
        }

        // 好友头像
        if (AvatarManager.Instance != null)
        {
            var friendUrl = AvatarManager.Instance.GetFriendAvatarUrl(friendId);
            if (!string.IsNullOrEmpty(friendUrl)) AvatarLoader.PreloadIntoMemory(friendUrl);
        }
    }

    private void ClearMessages()
    {
        if (messContent == null) return;
        for (int i = messContent.childCount - 1; i >= 0; i--)
        {
            Destroy(messContent.GetChild(i).gameObject);
        }
        _lastMessageTime = System.DateTime.MinValue;
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
                    AddMessage(resp.data.content, MessageSender.Me, null, 0, resp.data.createdAt);
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
        }
    }

    private void OnPrivateMessage(PrivateMessageEvent evt)
    {
        // 实时收到好友消息
        if (_activeFriendId > 0 && evt.FromUserId == _activeFriendId)
        {
            // 正在与该好友聊天：直接显示消息，视为已读
            AddMessage(evt.Content, MessageSender.Friend, evt.AvatarUrl, evt.FromUserId, evt.CreatedAt);
            ClearFriendUnread(evt.FromUserId);
        }
        else
        {
            // 未打开该会话：未读数 +1，点亮 Friend Button 的 Status 红点角标并显示数字
            AddUnread(evt.FromUserId);
            Debug.Log($"[Chat] 收到 {evt.FromUserId} 的消息（未打开该会话）：{evt.Content}");
        }
    }

    // ============ UI 渲染（保留原实现） ============

    private void AddMessage(string text, MessageSender sender = MessageSender.Me, string avatarUrl = null, long fromUserId = 0, long createdAt = 0)
    {
        // 消息真实时间：优先用服务端返回的 createdAt（UTC 毫秒时间戳），统一转 UTC+8 北京时间
        System.DateTime msgTime = createdAt > 0
            ? System.DateTimeOffset.FromUnixTimeMilliseconds(createdAt).ToOffset(System.TimeSpan.FromHours(8)).DateTime
            : System.DateTime.Now;

        // 检查是否需要显示时间：距上一条消息超过 3 分钟则插入时间标签
        if (ShouldShowTime(msgTime))
        {
            GameObject timeObj = Instantiate(timeLabelPrefab, messContent);
            TMP_Text timeText = timeObj.GetComponentInChildren<TMP_Text>();
            if (timeText != null) timeText.text = FormatTime(msgTime);
            _lastMessageTime = msgTime;
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

        // 设置头像：优先用消息携带的 avatarUrl，其次从全局缓存查，最后回退默认头像
        var avatarNode = msgObj.transform.Find("Avatar");
        if (avatarNode != null)
        {
            Image avatar = avatarNode.GetComponent<Image>();
            if (avatar != null)
            {
                string resolvedUrl = avatarUrl;
                if (string.IsNullOrEmpty(resolvedUrl) && AvatarManager.Instance != null)
                {
                    resolvedUrl = (sender == MessageSender.Me)
                        ? AvatarManager.Instance.GetMyAvatarUrl()
                        : AvatarManager.Instance.GetFriendAvatarUrl(fromUserId);
                }

                if (!string.IsNullOrEmpty(resolvedUrl))
                {
                    // 从服务器加载头像
                    string fullUrl = AvatarLoader.ToAbsoluteUrl(resolvedUrl);
                    AvatarLoader.LoadInto(avatar, fullUrl, this);
                }
                else
                {
                    // 使用默认头像
                    avatar.sprite = (sender == MessageSender.Me) ? myAvatar : friendAvatar;
                }
            }
        }
        ScrollToBottom();
    }

    private bool ShouldShowTime(System.DateTime now)
    {
        // 上一条消息时间为空（尚未发送过消息）时，第一条消息也显示时间
        if (_lastMessageTime == System.DateTime.MinValue) return true;

        System.TimeSpan diff = now - _lastMessageTime;
        return diff.TotalMinutes >= 3;
    }

    /// <summary>
    /// 格式化时间标签：今天显示 HH:mm；昨天显示「昨天 HH:mm」；更早显示具体日期 + HH:mm。
    /// </summary>
    private string FormatTime(System.DateTime time)
    {
        System.DateTime today = System.DateTime.Today;
        System.DateTime yesterday = today.AddDays(-1);

        string hm = time.ToString("HH:mm");
        if (time.Date == today)
        {
            return hm;
        }
        if (time.Date == yesterday)
        {
            return "昨天 " + hm;
        }
        // 同年显示 MM-dd，跨年显示 yyyy-MM-dd
        if (time.Year == today.Year)
        {
            return time.ToString("MM-dd") + " " + hm;
        }
        return time.ToString("yyyy-MM-dd") + " " + hm;
    }
}
