using UnityEngine;

/// <summary>
/// 全局音频管理器（单例）。
/// 负责：背景音乐（BGM）的播放、暂停、静音、音量控制，以及音效（SFX）播放预留位。
/// 挂载方式：添加到任意常驻场景物体（如 GameManager 所在物体），或由 start 场景手动创建。
/// 对应文档十五 Scripts/Core 约定。
/// </summary>
public class AudioManager : MonoBehaviour
{
    public static AudioManager Instance { get; private set; }

    [Header("BGM 资源")]
    [Tooltip("开始界面的背景音乐，可直接拖入；留空则尝试从 Resources/Audio/BGM 加载")]
    public AudioClip bgmClip;

    [Header("设置")]
    [Tooltip("背景音乐音量 (0~1)")]
    [Range(0f, 1f)]
    public float bgmVolume = 0.6f;

    [Tooltip("播放位资源加载路径（Resources 下），用于 bgmClip 为空时自动加载")]
    public string bgmResourcePath = "Audio/BGM/start_bgm";

    [Header("音效播放位")]
    [Tooltip("预留：音效资源目录（Resources 下），可扩展按钮点击音效等")]
    public string sfxResourcePath = "Audio/SFX";

    private AudioSource _bgmSource;

    /// <summary>当前是否处于静音（开关）状态。</summary>
    public bool IsMuted { get; private set; }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        DontDestroyOnLoad(gameObject);

        // 常驻音频源，播放背景音乐
        _bgmSource = gameObject.AddComponent<AudioSource>();
        _bgmSource.playOnAwake = false;
        _bgmSource.loop = true;
        _bgmSource.volume = bgmVolume;
    }

    private void Start()
    {
        // 预留播放位：若未手动指定 bgmClip，则尝试从 Resources 加载
        if (bgmClip == null)
        {
            var clip = Resources.Load<AudioClip>(bgmResourcePath);
            if (clip != null)
            {
                bgmClip = clip;
            }
        }

        // 默认不自动播放，由 MusicToggleButton 触发；如需进入即播放可调用 PlayBgm()
    }

    /// <summary>
    /// 播放背景音乐。已在播放则忽略。
    /// </summary>
    public void PlayBgm()
    {
        if (_bgmSource == null || bgmClip == null) return;
        if (!_bgmSource.isPlaying)
        {
            _bgmSource.clip = bgmClip;
            _bgmSource.Play();
        }
    }

    /// <summary>
    /// 暂停背景音乐。
    /// </summary>
    public void PauseBgm()
    {
        if (_bgmSource != null && _bgmSource.isPlaying)
        {
            _bgmSource.Pause();
        }
    }

    /// <summary>
    /// 切换背景音乐播放/暂停，返回切换后的状态（true=播放中）。
    /// </summary>
    public bool ToggleBgm()
    {
        if (_bgmSource == null || bgmClip == null) return false;

        if (_bgmSource.isPlaying)
        {
            _bgmSource.Pause();
            return false;
        }
        _bgmSource.clip = bgmClip;
        _bgmSource.Play();
        return true;
    }

    /// <summary>
    /// 设置静音开关，返回切换后的静音状态。
    /// </summary>
    public bool ToggleMute()
    {
        IsMuted = !IsMuted;
        if (_bgmSource != null)
        {
            _bgmSource.mute = IsMuted;
        }
        return IsMuted;
    }

    /// <summary>
    /// 设置背景音乐音量。
    /// </summary>
    public void SetBgmVolume(float volume)
    {
        bgmVolume = Mathf.Clamp01(volume);
        if (_bgmSource != null)
        {
            _bgmSource.volume = bgmVolume;
        }
    }

    /// <summary>
    /// 预留播放位：播放一次性音效（SFX）。
    /// 用法：AudioManager.Instance.PlaySfx("click");
    /// </summary>
    public void PlaySfx(string sfxName, float volumeScale = 1f)
    {
        var clip = Resources.Load<AudioClip>(sfxResourcePath + "/" + sfxName);
        if (clip == null) return;
        if (_bgmSource == null) return;

        // 用一个临时 AudioSource 播放短音效，播完自动销毁
        var go = new GameObject("Sfx_" + sfxName);
        DontDestroyOnLoad(go);
        var src = go.AddComponent<AudioSource>();
        src.clip = clip;
        src.volume = bgmVolume * volumeScale;
        src.mute = IsMuted;
        src.Play();
        Destroy(go, clip.length + 0.1f);
    }
}
