using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Video;

namespace WildCourt.UI
{
    /// <summary>
    /// 片头视频播放完成后自动跳转场景。
    /// 挂载到已有 VideoPlayer 的 GameObject 上（视频资源由场景里的 VideoPlayer 直接引用，方案 A）。
    /// 使用 VideoPlayer.loopPointReached 事件判断播放完成，无每帧轮询，符合 GC 规范。
    /// </summary>
    [RequireComponent(typeof(VideoPlayer))]
    public class VideoSceneTransition : MonoBehaviour
    {
        [Header("视频播放完成后跳转的目标场景名")]
        [Tooltip("例如 LoginScence")]
        public string nextSceneName = "LoginScence";

        private VideoPlayer _video;
        private bool _finished;

        private void Awake()
        {
            _video = GetComponent<VideoPlayer>();
        }

        private void Start()
        {
            if (_video == null)
            {
                Debug.LogWarning("[VideoSceneTransition] 未找到 VideoPlayer 组件");
                return;
            }

            // 方法组订阅，无 Lambda 分配
            _video.loopPointReached += OnVideoFinished;
        }

        private void OnDestroy()
        {
            if (_video != null)
            {
                _video.loopPointReached -= OnVideoFinished;   // 事件移除，防泄漏
            }
        }

        // 视频播放到结尾回调（未循环时触发一次）
        private void OnVideoFinished(VideoPlayer vp)
        {
            if (_finished) return;
            _finished = true;

            if (string.IsNullOrEmpty(nextSceneName))
            {
                Debug.LogWarning("[VideoSceneTransition] 未指定跳转场景名，跳过");
                return;
            }

            SceneManager.LoadScene(nextSceneName);
        }
    }
}
