using System.Collections.Generic;
using UnityEngine;
using WildCourt.Core;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.User
{
    /// <summary>
    /// 全局头像缓存管理器（单例）。
    /// <para>职责：登录/启动时缓存「自己的头像 URL」，各模块统一从这里读，
    /// 修改头像后经 <see cref="ProfileUpdatedEvent"/> 同步更新缓存；
    /// 好友头像由各列表模块（如 ChatManner.LoadFriendList）主动喂入缓存。</para>
    /// <para>各模块拿 URL 后，再用 <see cref="WildCourt.Utils.AvatarLoader"/> 下载图片显示。</para>
    /// </summary>
    public class AvatarManager : MonoBehaviour
    {
        public static AvatarManager Instance { get; private set; }

        private long _myUserId;
        private string _myAvatarUrl;
        private bool _profileLoaded;

        // userId -> avatarUrl（相对路径 /uploads/...，未拼绝对地址）
        private readonly Dictionary<long, string> _friendAvatars = new Dictionary<long, string>();

        /// <summary>
        /// 确保单例已创建（自举）。登录成功后调用，保证订阅事件 + 拉取头像缓存。
        /// </summary>
        public static void EnsureCreated()
        {
            if (Instance != null) return;
            var go = new GameObject("AvatarManager");
            DontDestroyOnLoad(go);
            go.AddComponent<AvatarManager>();
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

            EventBus.Subscribe<ProfileUpdatedEvent>(OnProfileUpdated);
            EnsureProfileLoaded();
        }

        private void OnDestroy()
        {
            EventBus.Unsubscribe<ProfileUpdatedEvent>(OnProfileUpdated);
        }

        /// <summary>本地是否已拿到自己的头像缓存（未加载前返回空）。</summary>
        public bool ProfileLoaded => _profileLoaded;

        /// <summary>当前登录用户 ID（-1 表示尚未加载）。</summary>
        public long MyUserId => _profileLoaded ? _myUserId : -1;

        /// <summary>自己的头像 URL（相对路径；未加载返回 null）。</summary>
        public string MyAvatarUrl => _myAvatarUrl;

        /// <summary>
        /// 获取自己的头像 URL。首次调用若尚未缓存，会发起 profile 拉取并缓存（异步，
        /// 本次返回 null，拿到后经 <see cref="ProfileUpdatedEvent"/> 通知）。
        /// </summary>
        public string GetMyAvatarUrl()
        {
            EnsureProfileLoaded();
            return _myAvatarUrl;
        }

        /// <summary>获取当前登录用户 ID（首次调用会触发 profile 拉取）。</summary>
        public long GetMyUserId()
        {
            EnsureProfileLoaded();
            return _myUserId;
        }

        /// <summary>获取指定好友的头像 URL（无缓存返回 null）。</summary>
        public string GetFriendAvatarUrl(long userId)
        {
            return _friendAvatars.TryGetValue(userId, out var url) ? url : null;
        }

        /// <summary>缓存好友头像（供好友列表等模块喂入）。</summary>
        public void SetFriendAvatar(long userId, string avatarUrl)
        {
            if (userId <= 0) return;
            if (string.IsNullOrEmpty(avatarUrl))
            {
                _friendAvatars.Remove(userId);
            }
            else
            {
                _friendAvatars[userId] = avatarUrl;
            }
        }

        /// <summary>清空好友头像缓存（登出或刷新时调用）。</summary>
        public void ClearFriendAvatars()
        {
            _friendAvatars.Clear();
        }

        /// <summary>确保自己的 profile 已拉取（幂等；网络未就绪则跳过，下次调用重试）。</summary>
        private void EnsureProfileLoaded()
        {
            if (_profileLoaded || NetManager.Instance == null) return;
            _profileLoaded = true; // 先置位，避免并发重复拉取
            NetManager.Instance.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp =>
                {
                    if (resp != null && resp.data != null)
                    {
                        _myUserId = resp.data.userId;
                        _myAvatarUrl = resp.data.avatarUrl;
                    }
                },
                (code, msg) =>
                {
                    // 拉取失败允许下次重试
                    _profileLoaded = false;
                    Debug.LogWarning($"[AvatarManager] 拉取玩家资料失败：{msg}");
                });
        }

        /// <summary>头像/昵称修改后同步缓存（UserPanel 修改头像/昵称时广播）。</summary>
        private void OnProfileUpdated(ProfileUpdatedEvent evt)
        {
            if (!string.IsNullOrEmpty(evt.AvatarUrl))
            {
                _myAvatarUrl = evt.AvatarUrl;
            }
        }
    }
}
