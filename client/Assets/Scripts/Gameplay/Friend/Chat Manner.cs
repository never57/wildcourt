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

    [Header("消息列表")]
    public Transform messContent; // 存放消息
    public GameObject myMessagePrefab;  // 本人消息预制体
    public GameObject friendMessagePrefab;  // 好友消息预制体

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

        // 打开时刷新好友列表
        LoadFriendList();
    }

    public void ChatClose()
    {
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
        if (labelAvatarImage != null && !string.IsNullOrEmpty(evt.AvatarUrl))
        {
            AvatarLoader.LoadInto(labelAvatarImage, evt.AvatarUrl, this);
        }
        if (labelNameText != null && !string.IsNullOrEmpty(evt.Nickname))
        {
            labelNameText.text = evt.Nickname;
        }
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
                if (resp != null && resp.data != null && resp.data.friends != null)
                {
                    _friends.AddRange(resp.data.friends);
                    // 把好友头像喂入全局缓存，供消息头像与其它模块复用
                    if (AvatarManager.Instance != null)
                    {
                        foreach (var f in resp.data.friends)
                        {
                            AvatarManager.Instance.SetFriendAvatar(f.userId, f.avatarUrl);
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

            // 好友头像：按玩家 ID 从 AvatarManager 缓存取（LoadFriendList 已喂入缓存），再加载显示。
            // 头像 URL 现已带版本号（/uploads/avatar/{userId}-{ts}.png），好友换头像 URL 会变化，
            // LoadInto 按 URL 判缓存自然重下，无需 force，也避免每次打开都重新下载。
            BindFriendAvatar(go, f.userId, f.avatarUrl);

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
        // 实时收到好友消息：仅当正在与该好友聊天时显示，否则忽略（好友列表可加未读角标，暂略）
        if (_activeFriendId > 0 && evt.FromUserId == _activeFriendId)
        {
            AddMessage(evt.Content, MessageSender.Friend, evt.AvatarUrl, evt.FromUserId, evt.CreatedAt);
        }
        else
        {
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
