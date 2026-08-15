using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Serialization;
using WildCourt.Core;
using WildCourt.Lua;
using WildCourt.Net;
using WildCourt.Net.Protocol;

#if USE_XLUA
using XLua;
#endif

namespace WildCourt.UI.Letters
{
    /// <summary>
    /// 信箱面板（预制体驱动）：UI 结构全部由 MailPanel.prefab 在编辑器中搭建
    /// （本类不做任何代码建 UI），代码只负责实例化预制体、绑定字段与数据刷新。
    /// 玩法逻辑由 Lua MailManager 持有（Send/Claim），结果经 WC.event
    /// 的 Mail.* 事件回传刷新。
    ///
    /// 预制体搭建要求（Unity 会话按此创建 MailPanel.prefab）：
    ///   根节点挂 MailPanel；序列化字段：panelRoot/titleText/closeButton/
    ///   listRoot/itemContainer/mailItemPrefab/emptyText/detailRoot/fromText/
    ///   contentText/itemText/claimButton/deleteReadButton/composeRoot/
    ///   contentInputText/sendButton/cancelButton；
    ///   mailItemPrefab 挂 MailItem（fromText/contentText/statusText/timeText/button）。
    /// </summary>
    public class MailPanel : MonoBehaviour
    {
        public static MailPanel Instance { get; private set; }

        /// <summary>跨场景注入的 MailPanel 预制体（登录时注入，支持热更）。</summary>
        public static GameObject MailPanelPrefab { get; set; }

        [Header("预制体绑定（MailPanel.prefab，编辑器搭建）")]
        [SerializeField] private GameObject panelRoot;
        [SerializeField] private Text titleText;
        [SerializeField] private Button closeButton;
        [SerializeField] private Transform listRoot;
        [SerializeField] private Transform itemContainer;
        [SerializeField] private GameObject mailItemPrefab;
        [SerializeField] private Text emptyText;
        [SerializeField] private GameObject detailRoot;
        [SerializeField] private Text detailTitleText;
        [SerializeField] private Text fromText;
        [SerializeField] private Text contentText;
        [SerializeField] private Text itemText;
        [SerializeField] private Text tipText;
        [SerializeField, FormerlySerializedAs("giftBackButton")] private Button claimButton;
        [SerializeField, FormerlySerializedAs("backButton")] private Button deleteReadButton;

        [Header("附件格子（DetailRoot 内，展示随附物品）")]
        [SerializeField] private RectTransform attachmentRoot;   // 附件容器（运行时实例化 Items 格子挂在这里）
        [SerializeField] private GameObject attachmentPrefab;    // Items 预制体（MailAttachmentItem 组件）

        private readonly List<MailItem> _items = new();
        private LetterItem _selected;
        private bool _subscribed;
        private Coroutine _tipCoroutine;
        private bool _pendingSelectAfterDelete;   // 删除后自动跳转标记
        private int _preferIndex = -1;            // 删除项在原列表中的位置，删除后选中该位置的项
        private readonly List<MailAttachmentItem> _attachmentSlots = new();  // 附件格子实例池（复用）

        public static MailPanel CreateOrInstantiate()
        {
            if (MailPanelPrefab == null)
            {
                Debug.LogWarning("[MailPanel] 未注入 MailPanel 预制体（登录时注入 mailPanelPrefab），跳过打开");
                return null;
            }
            if (Instance != null)
            {
                Instance.Open();
                return Instance;
            }
            var go = Instantiate(MailPanelPrefab);
            var panel = go.GetComponent<MailPanel>();
            if (panel == null)
            {
                Destroy(go);
                Debug.LogWarning("[MailPanel] 预制体缺少 MailPanel 组件");
                return null;
            }
            return panel;
        }

        public void Open()
        {
            if (panelRoot != null) panelRoot.SetActive(true);
            ShowList();
        }

        public void Close()
        {
            if (panelRoot != null) panelRoot.SetActive(false);
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
        }

        private void Start()
        {
            SubscribeEvents();
            if (closeButton != null) closeButton.onClick.AddListener(Close);
            if (claimButton != null) claimButton.onClick.AddListener(Claim);
            if (deleteReadButton != null) deleteReadButton.onClick.AddListener(DeleteRead);
            RefreshInbox();
        }

        // ---------- 事件订阅（结果经 WC.event 回传） ----------

        private void SubscribeEvents()
        {
            if (_subscribed) return;
            _subscribed = true;
#if USE_XLUA
            LuaEventBridge.Subscribe("Mail.InboxResult", OnInboxResult);
            LuaEventBridge.Subscribe("Mail.ClaimResult", OnClaimResult);
            LuaEventBridge.Subscribe("Mail.DeleteResult", OnDeleteResult);
            LuaEventBridge.Subscribe("Mail.Arrived", OnMailArrived);
#endif
        }

#if USE_XLUA
        private void OnInboxResult(LuaTable payload)
        {
            if (listRoot == null) return;
            foreach (var item in _items) Destroy(item.gameObject);
            _items.Clear();

            var letters = payload.Get<object>("letters");
            if (letters is LuaTable table)
            {
                table.ForEach<object, object>((k, v) =>
                {
                    if (v is LuaTable row)
                    {
                        var go = Instantiate(mailItemPrefab, itemContainer);
                        var item = go.GetComponent<MailItem>();
                        if (item == null)
                        {
                            Destroy(go);
                            return;
                        }
                        var letter = new LetterItem
                        {
                            letterId = Long(row.Get<object>("letterId")),
                            fromUserId = Long(row.Get<object>("fromUserId")),
                            fromNickname = Str(row.Get<object>("fromNickname")),
                            title = Str(row.Get<object>("title")),
                            content = Str(row.Get<object>("content")),
                            // 附件在 Lua 降级分支暂不解析（官方信件链路走 C# REST，见 RenderInbox）
                            attachments = null,
                            status = Str(row.Get<object>("status")),
                            createdAt = Long(row.Get<object>("createdAt")),
                        };
                        item.Bind(letter, OnLetterClicked);
                        _items.Add(item);
                    }
                });
            }
            bool empty = _items.Count == 0;
            if (emptyText != null) emptyText.gameObject.SetActive(empty);
        }

        private void OnClaimResult(LuaTable payload)
        {
            bool ok = Bool(payload.Get<object>("ok"));
            SetHint(ok ? "领取成功" : "领取失败: " + Str(payload.Get<object>("msg")));
            if (ok) ShowList();
        }

        private void OnDeleteResult(LuaTable payload)
        {
            bool ok = Bool(payload.Get<object>("ok"));
            SetHint(ok ? "已删除" : "删除失败: " + Str(payload.Get<object>("msg")));
            if (ok) OnDeleteDone();
        }

        private void OnMailArrived(LuaTable payload)
        {
            SetHint("收到新信件");
            RefreshInbox();
        }
#endif

        // ---------- 视图切换 ----------

        private void ShowList()
        {
            // 左右分栏常驻：列表一直显示，仅刷新数据，不隐藏详情
            RefreshInbox();
        }

        private void OnLetterClicked(LetterItem letter)
        {
            _selected = letter;
            // 只更新右侧详情内容，不隐藏左侧列表
            if (detailRoot != null) detailRoot.SetActive(true);
            // 详情区标题：优先 title，为空回退 content
            var title = string.IsNullOrEmpty(letter.title) ? letter.content : letter.title;
            if (detailTitleText != null) detailTitleText.text = title;
            if (fromText != null) fromText.text = "发件人：" + (letter.fromUserId == 0 ? "系统管理员" : letter.fromNickname);
            if (contentText != null) contentText.text = letter.content;
            // itemText 作为附件区标题，固定显示"附件"
            if (itemText != null) itemText.text = "附件";

            // 附件格子：无附件隐藏，有附件显示并绑定图标/名称/数量
            BindAttachment(letter);

            // 点击即标记已读：未读 → 已读（领取/删除不影响该转换）
            if (letter.status == "未读")
            {
                MarkRead(letter);
            }
        }

        /// <summary>绑定附件格子：无附件隐藏容器；有附件按数量实例化多个 Items 格子并填充。</summary>
        private void BindAttachment(LetterItem letter)
        {
            if (attachmentRoot == null) return;

            var attachments = letter.attachments;
            bool hasReward = attachments != null && attachments.Length > 0;
            attachmentRoot.gameObject.SetActive(hasReward);
            if (!hasReward) return;

            if (attachmentPrefab == null) return;

            // 确保附件容器有网格布局（每行 5 个，动态数量自动换行，类似背包）
            EnsureAttachmentLayout();

            // 确保格子实例数量足够（复用已有，不足补实例化，多余隐藏）
            int need = attachments.Length;
            while (_attachmentSlots.Count < need)
            {
                var go = Instantiate(attachmentPrefab, attachmentRoot);
                var slot = go.GetComponent<MailAttachmentItem>();
                if (slot == null)
                {
                    Destroy(go);
                    break;
                }
                _attachmentSlots.Add(slot);
            }

            for (int i = 0; i < _attachmentSlots.Count; i++)
            {
                var slot = _attachmentSlots[i];
                if (i < need)
                {
                    slot.gameObject.SetActive(true);
                    bool claimed = letter != null && letter.status == "已领取";
                    slot.Bind(attachments[i].type, attachments[i].count, attachments[i].itemId, claimed);
                }
                else
                {
                    slot.gameObject.SetActive(false);
                }
            }
        }

        /// <summary>
        /// 确保附件容器带网格布局（每行 5 个，动态数量自动换行，类似背包）。
        /// 用代码动态添加避免在 prefab 里手写 GridLayoutGroup 序列化导致布局不匹配。
        /// </summary>
        private void EnsureAttachmentLayout()
        {
            if (attachmentRoot == null) return;

            var grid = attachmentRoot.GetComponent<GridLayoutGroup>();
            if (grid == null)
            {
                grid = attachmentRoot.gameObject.AddComponent<GridLayoutGroup>();
            }

            grid.cellSize = new Vector2(100f, 100f);   // 格子 100x100
            grid.spacing = new Vector2(10f, 10f);      // 间距 10
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            grid.childAlignment = TextAnchor.UpperLeft;
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = 5;                  // 每行 5 个
        }

        /// <summary>标记信件已读（未读 → 已读），成功后更新本地状态并刷新对应列表项。</summary>
        private void MarkRead(LetterItem letter)
        {
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Post<LetterStatusResponse>(ApiPaths.LetterMarkRead,
                    new ClaimLetterRequest { letterId = letter.letterId },
                    resp =>
                    {
                        letter.status = "已读";
                        RefreshItemStatus(letter);
                    },
                    (code, msg) => { /* 已读标记失败不打断阅读，静默忽略 */ });
            }
        }

        /// <summary>按 letter 引用刷新对应列表项的 statusText（避免整列表重建）。</summary>
        private void RefreshItemStatus(LetterItem letter)
        {
            foreach (var item in _items)
            {
                if (item != null && item.Letter != null && item.Letter.letterId == letter.letterId)
                {
                    item.Bind(letter, OnLetterClicked);
                    break;
                }
            }
        }

        // ---------- 交互 ----------

        private void Claim()
        {
            if (_selected == null) return;
            if (_selected.status == "已领取")
            {
                SetHint("已经领取过了哦~");
                return;
            }
            if (_selected.attachments == null || _selected.attachments.Length == 0)
            {
                SetHint("该邮件没有附带物品");
                return;
            }
            long letterId = _selected.letterId;
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Post<ClaimResponse>(ApiPaths.LetterClaim,
                    new ClaimLetterRequest { letterId = letterId },
                    resp =>
                    {
                        // 领取成功后同步本地状态为「已领取」，否则再次点击时 _selected.status
                        // 仍是旧值（已读），无法命中「已经领取过了」的幂等提示，会重复调领取接口。
                        _selected.status = "已领取";
                        RefreshItemStatus(_selected);
                        SetHint("领取成功");

                        // 领取附件后广播货币变化，通知建筑/农场等依赖庭贝显示的 UI 刷新。
                        // 服务端 ClaimResponse 不含权威余额，故通过事件触发 YardMainUI 重拉 profile。
                        NotifyCurrencyChanged(resp.data);
                        ShowList();
                    },
                    (code, msg) => SetHint("领取失败: " + msg));
            }
        }

        /// <summary>
        /// 领取附件后广播货币变化事件，通知依赖庭贝/邮券显示的 UI（建筑/农场等）刷新。
        /// 由于服务端 ClaimResponse 不含权威余额，仅做本地预估 + 触发订阅方重拉 profile 对齐权威值。
        /// </summary>
        private void NotifyCurrencyChanged(ClaimResponse resp)
        {
            // 本地预估：庭贝/邮券按领取数量累加（遍历多附件，type 决定归属）
            long shellDelta = 0;
            long couponDelta = 0;
            if (resp != null && resp.attachments != null)
            {
                foreach (var att in resp.attachments)
                {
                    if (att.type == "shell") shellDelta += att.count;
                    else if (att.type == "coupon") couponDelta += att.count;
                }
            }

            // 通过 EventBus 广播：订阅方（YardMainUI 等）收到后重拉 profile 对齐权威余额
            EventBus.Publish(new CurrencyChangedEvent
            {
                Shell = shellDelta,
                Coupon = couponDelta,
            });
        }

        /// <summary>删除已读信件（删除后自动打开下一封/回列表）。</summary>
        private void DeleteRead()
        {
            if (_selected == null) return;

            // 附件有物品且未领取时不允许删除：先领取附件（或明确放弃）才能删除，防止误删丢失奖励
            if (_selected.attachments != null && _selected.attachments.Length > 0 && _selected.status != "已领取")
            {
                SetHint("该信件有未领取的附件，请先领取");
                return;
            }

            long letterId = _selected.letterId;
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Delete<LetterStatusResponse>(ApiPaths.LetterDelete(letterId),
                    resp => { SetHint("已删除"); OnDeleteDone(); },
                    (code, msg) => SetHint("删除失败: " + msg));
            }
        }

        /// <summary>删除成功后刷新，并自动跳转到相邻信件。</summary>
        private void OnDeleteDone()
        {
            // 记录删除项在原列表中的位置，刷新后选中同位置（视觉上顶替它）的信件
            _preferIndex = -1;
            if (_selected != null)
            {
                for (int i = 0; i < _items.Count; i++)
                {
                    if (_items[i] != null && _items[i].Letter != null && _items[i].Letter.letterId == _selected.letterId)
                    {
                        _preferIndex = i;
                        break;
                    }
                }
            }
            _selected = null;
            _pendingSelectAfterDelete = true;
            ShowList();
        }

        // ---------- 刷新 ----------

        private void RefreshInbox()
        {
            // 邮箱仅官方发放链路，直接走 C# REST（Lua 侧 Logic.Mail.MailManager 未实现，不做 Lua 优先）。
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Get<InboxResponse>(ApiPaths.LetterInbox,
                    resp => RenderInbox(resp.data != null ? resp.data.letters : null),
                    (code, msg) => SetHint("收件箱加载失败: " + msg));
            }
        }

        private void RenderInbox(LetterItem[] letters)
        {
            if (listRoot == null) return;

            // 关闭布局组件，避免逐个 Instantiate 时反复重排导致条目「飞入」
            var layout = itemContainer != null ? itemContainer.GetComponent<UnityEngine.UI.VerticalLayoutGroup>() : null;
            if (layout != null) layout.enabled = false;

            foreach (var item in _items) Destroy(item.gameObject);
            _items.Clear();

            if (letters == null || letters.Length == 0)
            {
                if (emptyText != null) emptyText.gameObject.SetActive(true);
                if (layout != null) layout.enabled = true;
                return;
            }
            if (emptyText != null) emptyText.gameObject.SetActive(false);
            foreach (var letter in letters)
            {
                var go = Instantiate(mailItemPrefab, itemContainer);
                var item = go.GetComponent<MailItem>();
                if (item == null)
                {
                    Destroy(go);
                    continue;
                }
                item.Bind(letter, OnLetterClicked);
                _items.Add(item);
            }

            // 全部实例化完成后一次性重排布局
            if (layout != null) layout.enabled = true;

            // 自动选中逻辑：
            // 1) 删除后跳转：按删除项相邻位置选中一封
            // 2) 打开信箱无选中：自动选中第一封
            if (_pendingSelectAfterDelete)
            {
                _pendingSelectAfterDelete = false;
                AutoSelectAfterDelete(letters);
            }
            else if (_selected == null && letters.Length > 0)
            {
                OnLetterClicked(letters[0]);
            }
        }

        /// <summary>删除后自动选中相邻信件：优先选中原删除项位置的项，越界选最后一封。</summary>
        private void AutoSelectAfterDelete(LetterItem[] letters)
        {
            if (letters == null || letters.Length == 0)
            {
                // 无剩余信件：清空详情
                if (detailRoot != null) detailRoot.SetActive(false);
                _selected = null;
                return;
            }

            // 删除后列表少一项，原 index 处的项（如果存在）即视觉上顶替删除项的那一封
            int idx = _preferIndex;
            if (idx < 0 || idx >= letters.Length)
            {
                idx = letters.Length - 1; // 越界（如删的是最后一封）则选新的最后一封
            }
            OnLetterClicked(letters[idx]);
        }

        private void SetHint(string msg)
        {
            if (tipText == null) return;

            // 显示 Tip 节点并设置文字
            tipText.text = msg;
            if (tipText.gameObject != null) tipText.gameObject.SetActive(true);

            // 取消上一次的隐藏计时，重新计时 1.5 秒后隐藏
            if (_tipCoroutine != null) StopCoroutine(_tipCoroutine);
            _tipCoroutine = StartCoroutine(HideTipAfterDelay(1.5f));
        }

        private IEnumerator HideTipAfterDelay(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (tipText != null && tipText.gameObject != null)
            {
                tipText.gameObject.SetActive(false);
            }
            _tipCoroutine = null;
        }

        // ---------- Lua 值转换 ----------

#if USE_XLUA
        private static string Str(object v) => v == null ? string.Empty : v.ToString();
        private static long Long(object v) => v == null ? 0L : System.Convert.ToInt64(v);
        private static bool Bool(object v) => v is bool b && b;
#endif
    }
}
