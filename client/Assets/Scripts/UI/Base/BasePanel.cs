using UnityEngine;

namespace WildCourt.UI.Base
{
    /// <summary>
    /// 界面基类（文档 03 §3.1 UI/Base）：统一面板生命周期
    /// <c>OnInit → OnOpen → OnClose → OnDispose</c>。
    /// <para>UI 数据来源：EventBus 事件 + 服务端快照缓存（文档 03 §3.7），面板不做服务端判定；
    /// Lua 面板经 <see cref="WildCourt.Lua.LuaBehaviour"/> 绑定同构生命周期（lua/UI/，可热更）。</para>
    /// </summary>
    public abstract class BasePanel : MonoBehaviour
    {
        /// <summary>面板标识（默认取类型名，可覆写为资源路径）。</summary>
        public virtual string PanelName => GetType().Name;

        public bool IsOpen { get; private set; }

        /// <summary>面板数据绑定：获取服务端快照缓存、订阅 EventBus（注意在 <see cref="OnDispose"/> 反订阅）。</summary>
        public virtual void OnInit()
        {
        }

        public virtual void OnOpen()
        {
            IsOpen = true;
            gameObject.SetActive(true);
        }

        public virtual void OnClose()
        {
            IsOpen = false;
            gameObject.SetActive(false);
        }

        public virtual void OnDispose()
        {
            Destroy(gameObject);
        }
    }
}
