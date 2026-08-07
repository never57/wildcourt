using UnityEngine;

namespace WildCourt.UI
{
    /// <summary>
    /// 登录场景引导器。优先使用 VideoSceneTransition 预加载的 Prefab，否则使用序列化引用。
    /// 挂载在 LoginScence 的任意 GameObject 上。
    /// </summary>
    public class LoginSceneBootstrap : MonoBehaviour
    {
        [Tooltip("拖入 LoginUI Prefab（支持热更）")]
        public GameObject loginUIPrefab;

        private void Start()
        {
            if (VideoSceneTransition.PreloadedLoginUI != null)
            {
                Instantiate(VideoSceneTransition.PreloadedLoginUI);
                VideoSceneTransition.PreloadedLoginUI = null;
                return;
            }

            if (loginUIPrefab != null)
                Instantiate(loginUIPrefab);
        }
    }
}
