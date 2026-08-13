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
        [SerializeField] private Text fromText;
        [SerializeField] private Text contentText;
        [SerializeField] private Text itemText;
        [SerializeField] private Text tipText;
        [SerializeField, FormerlySerializedAs("giftBackButton")] private Button claimButton;
        [SerializeField, FormerlySerializedAs("backButton")] private Button deleteReadButton;

        private readonly List<MailItem> _items = new();
        private LetterItem _selected;
        private bool _subscribed;
        private Coroutine _tipCoroutine;

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
                            content = Str(row.Get<object>("content")),
                            itemId = Str(row.Get<object>("itemId")),
                            rewardType = Str(row.Get<object>("rewardType")),
                            rewardCount = (int)Long(row.Get<object>("rewardCount")),
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
            if (fromText != null) fromText.text = "寄件人：" + (letter.fromUserId == 0 ? "官方" : letter.fromNickname);
            if (contentText != null) contentText.text = letter.content;
            if (itemText != null) itemText.text = DescribeReward(letter);
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
            if (string.IsNullOrEmpty(_selected.rewardType))
            {
                SetHint("该邮件没有附带物品");
                return;
            }
            long letterId = _selected.letterId;
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Post<ClaimResponse>(ApiPaths.LetterClaim,
                    new ClaimLetterRequest { letterId = letterId },
                    resp => { SetHint("领取成功"); ShowList(); },
                    (code, msg) => SetHint("领取失败: " + msg));
            }
        }

        /// <summary>删除已读信件（删除后自动打开下一封/回列表）。</summary>
        private void DeleteRead()
        {
            if (_selected == null) return;
            long letterId = _selected.letterId;
            if (NetManager.Instance != null)
            {
                NetManager.Instance.Delete<LetterStatusResponse>(ApiPaths.LetterDelete(letterId),
                    resp => { SetHint("已删除"); OnDeleteDone(); },
                    (code, msg) => SetHint("删除失败: " + msg));
            }
        }

        /// <summary>删除成功后刷新，停留在列表页。</summary>
        private void OnDeleteDone()
        {
            _selected = null;
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

        /// <summary>附件描述：按 rewardType 展示庭贝/邮券/道具/非遗/无附件。</summary>
        private static string DescribeReward(LetterItem letter)
        {
            switch (letter.rewardType)
            {
                case "shell": return "随附庭贝：" + letter.rewardCount;
                case "coupon": return "随附邮券：" + letter.rewardCount;
                case "item": return "随附道具：" + (string.IsNullOrEmpty(letter.itemId) ? "未知" : letter.itemId) + " ×" + letter.rewardCount;
                default: return "随附附件：无";
            }
        }

        // ---------- Lua 值转换 ----------

#if USE_XLUA
        private static string Str(object v) => v == null ? string.Empty : v.ToString();
        private static long Long(object v) => v == null ? 0L : System.Convert.ToInt64(v);
        private static bool Bool(object v) => v is bool b && b;
#endif
    }
}
