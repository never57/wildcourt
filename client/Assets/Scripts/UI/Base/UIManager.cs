using System.Collections.Generic;
using UnityEngine;

namespace WildCourt.UI.Base
{
    /// <summary>
    /// 面板管理器：打开/关闭/缓存 UI 面板（文档 03 §3.1 UI 层）。
    /// <para>简化框架版：面板预制体由 UI 在场景/Resources 中提供，UIManager 负责实例化与生命周期驱动；
    /// 各系统面板（YardPanel / CookingPanel ...）继承 <see cref="BasePanel"/> 落位于 Scripts/UI/{系统}/。</para>
    /// </summary>
    public class UIManager : MonoBehaviour
    {
        public static UIManager Instance { get; private set; }

        [SerializeField] private Transform panelRoot;

        private readonly Dictionary<string, BasePanel> _opened = new Dictionary<string, BasePanel>();

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);
            if (panelRoot == null) panelRoot = transform;
        }

        /// <summary>打开面板（实例化 prefab 到根节点并驱动 OnInit/OnOpen）。</summary>
        public T Open<T>(GameObject prefab) where T : BasePanel
        {
            if (prefab == null)
            {
                Debug.LogError($"[UIManager] 打开 {typeof(T).Name} 失败：prefab 为空");
                return null;
            }
            string key = typeof(T).Name;
            if (_opened.TryGetValue(key, out var existing) && existing != null)
            {
                existing.OnOpen();
                return existing as T;
            }

            GameObject go = Instantiate(prefab, panelRoot);
            go.name = prefab.name;
            var panel = go.GetComponent<T>();
            if (panel == null)
            {
                Debug.LogError($"[UIManager] prefab 缺少 {typeof(T).Name} 组件");
                Destroy(go);
                return null;
            }

            panel.OnInit();
            panel.OnOpen();
            _opened[key] = panel;
            return panel;
        }

        /// <summary>关闭面板（不销毁，保留缓存复用）。</summary>
        public void Close<T>() where T : BasePanel
        {
            CloseByType(typeof(T));
        }

        public void Close(BasePanel panel)
        {
            if (panel == null) return;
            panel.OnClose();
            if (_opened.ContainsKey(panel.PanelName)) _opened.Remove(panel.PanelName);
        }

        private void CloseByType(System.Type type)
        {
            string key = type.Name;
            if (_opened.TryGetValue(key, out var panel))
            {
                panel.OnClose();
                _opened.Remove(key);
            }
        }

        /// <summary>关闭全部面板（场景切换时）。</summary>
        public void CloseAll()
        {
            foreach (var panel in _opened.Values)
            {
                if (panel != null) panel.OnClose();
            }
            _opened.Clear();
        }
    }
}
