using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net;
using WildCourt.Net.Protocol;
using WildCourt.Utils;

namespace WildCourt.UI.Friends
{
    /// <summary>
    /// 好友申请信息条目（application.prefab）。
    /// Avatar=申请人头像、Name=昵称、describe=个性标签（空则显示默认文案）、
    /// agree=接受、refuse=拒绝。
    /// </summary>
    public class ApplicationItem : MonoBehaviour
    {
        [Header("显示")]
        public Image avatarImage;          // Avatar
        public TMP_Text nameText;          // Name
        public TMP_Text describeText;      // describe

        [Header("操作")]
        public Button agreeButton;         // agree
        public Button refuseButton;        // refuse

        private long _fromUserId;

        /// <summary>绑定一条好友申请数据。onHandled 在同意/拒绝成功后回调（用于刷新列表）。</summary>
        public void Bind(FriendRequestItem item, Action onHandled)
        {
            if (item == null) return;
            _fromUserId = item.fromUserId;

            if (nameText != null) nameText.text = string.IsNullOrEmpty(item.fromNickname) ? item.fromUserId.ToString() : item.fromNickname;

            // 个性标签：为空显示默认文案
            if (describeText != null)
            {
                describeText.text = string.IsNullOrEmpty(item.signature)
                    ? "该玩家很神秘，什么也没有留下~"
                    : item.signature;
            }

            // 头像
            if (avatarImage != null)
            {
                if (!string.IsNullOrEmpty(item.avatarUrl))
                {
                    AvatarLoader.LoadInto(avatarImage, item.avatarUrl, this);
                }
            }

            // 按钮事件（先清再绑，避免重复实例化时叠加）
            if (agreeButton != null)
            {
                agreeButton.onClick.RemoveAllListeners();
                agreeButton.onClick.AddListener(() => Accept(onHandled));
            }
            if (refuseButton != null)
            {
                refuseButton.onClick.RemoveAllListeners();
                refuseButton.onClick.AddListener(() => Refuse(onHandled));
            }
        }

        private void Accept(Action onHandled)
        {
            if (NetManager.Instance == null) return;
            NetManager.Instance.Post<object>(ApiPaths.FriendAccept,
                new FriendApplyRequest { userId = _fromUserId },
                resp =>
                {
                    Debug.Log($"[ApplicationItem] 已接受 {_fromUserId} 的好友申请");
                    onHandled?.Invoke();
                },
                (code, msg) => Debug.LogWarning($"[ApplicationItem] 接受失败：{msg}"));
        }

        private void Refuse(Action onHandled)
        {
            if (NetManager.Instance == null) return;
            NetManager.Instance.Post<object>(ApiPaths.FriendReject,
                new FriendApplyRequest { userId = _fromUserId },
                resp =>
                {
                    Debug.Log($"[ApplicationItem] 已拒绝 {_fromUserId} 的好友申请");
                    onHandled?.Invoke();
                },
                (code, msg) => Debug.LogWarning($"[ApplicationItem] 拒绝失败：{msg}"));
        }
    }
}
