using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace WildCourt.UI
{
    /// <summary>
    /// 片头视频控制器：播放期间预加载 LoginUI Prefab，播放完成后切场景。
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoSceneTransition : MonoBehaviour
    {
        [Tooltip("例如 LoginScence")]
        public string nextSceneName = "LoginScence";

        /// <summary>预加载完成的 LoginUI Prefab，LoginSceneBootstrap 会直接使用。</summary>
        public static GameObject PreloadedLoginUI { get; set; }

        [Tooltip("拖入 LoginUI Prefab")]
        public GameObject loginUIPrefab;

        private VideoPlayer _video;
        private bool _finished;

        private void Awake()
        {
            _video = GetComponent<VideoPlayer>();
            PreloadedLoginUI = loginUIPrefab;
        }

        private void Start()
        {
            if (_video != null)
                _video.loopPointReached += OnVideoFinished;
        }

        private void OnDestroy()
        {
            if (_video != null)
                _video.loopPointReached -= OnVideoFinished;
        }

        private void OnVideoFinished(VideoPlayer vp)
        {
            if (_finished) return;
            _finished = true;
            SceneManager.LoadScene(nextSceneName);
        }
    }
}
