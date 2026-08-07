using UnityEngine;

namespace WildCourt.Gameplay
{
    /// <summary>
    /// Gameplay 模块管理器公共基类：单例 + 常驻。
    /// 各系统管理器（Crop/Processing/Cooking/...）继承本类，见文档 03 §3.2。
    /// </summary>
    public abstract class GameplayManager<T> : MonoBehaviour where T : GameplayManager<T>
    {
        public static T Instance { get; private set; }

        protected virtual void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = (T)this;
            DontDestroyOnLoad(gameObject);
        }
    }
}
