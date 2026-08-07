using UnityEngine;
using UnityEngine.UI;

namespace WlidCourt.UI
{
    /// <summary>
    /// 音乐开关按钮。
    /// 负责：点击时切换背景音乐播放/静音，并同步切换图标（播放/静音两态）。
    /// 挂载在开始界面的音乐按钮（Button）上，Icon 为按钮内的 Image。
    /// </summary>
    public class MusicToggleButton : MonoBehaviour
    {
        [Header("按钮引用")]
        [Tooltip("按钮内的图标 Image")]
        public Image icon;

        [Header("图标")]
        [Tooltip("播放中图标（例如音乐喇叭）")]
        public Sprite playIcon;
        [Tooltip("静音图标（例如带叉的喇叭），可留空则不切图只切透明度")]
        public Sprite muteIcon;

        [Header("行为")]
        [Tooltip("进入界面时自动开始播放背景音乐")]
        public bool autoPlayOnStart = true;

        private Button _button;
        private bool _isOn = true; // true=播放中

        private void Awake()
        {
            _button = GetComponent<Button>();
        }

        private void Start()
        {
            if (_button != null)
            {
                _button.onClick.AddListener(OnClick);
            }

            if (autoPlayOnStart && AudioManager.Instance != null)
            {
                // 默认开始播放背景音乐
                AudioManager.Instance.PlayBgm();
                _isOn = true;
            }
            else
            {
                _isOn = AudioManager.Instance != null && !AudioManager.Instance.IsMuted;
            }
            RefreshIcon();
        }

        private void OnDestroy()
        {
            if (_button != null)
            {
                _button.onClick.RemoveListener(OnClick);
            }
        }

        private void OnClick()
        {
            if (AudioManager.Instance == null) return;

            // 静音开关：静音时暂停播放，取消静音时恢复播放
            bool muted = AudioManager.Instance.ToggleMute();
            if (muted)
            {
                AudioManager.Instance.PauseBgm();
                _isOn = false;
            }
            else
            {
                AudioManager.Instance.PlayBgm();
                _isOn = true;
            }
            RefreshIcon();
        }

        /// <summary>
        /// 根据播放状态切换图标（有静音图标则切图，否则用透明度表达）。
        /// </summary>
        private void RefreshIcon()
        {
            if (icon == null) return;

            if (muteIcon != null)
            {
                icon.sprite = _isOn ? playIcon : muteIcon;
                icon.color = Color.white;
            }
            else
            {
                // 无静音图标时：播放=不透明，静音=半透明
                icon.color = _isOn ? new Color(1f, 1f, 1f, 1f) : new Color(1f, 1f, 1f, 0.4f);
            }
        }
    }
}
