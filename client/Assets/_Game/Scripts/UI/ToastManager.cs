using System.Collections.Generic;
using DG.Tweening;
using UnityEngine;
using UnityEngine.UI;

namespace Game.UI
{
    /// <summary>
    /// Toast 管理器：对象池复用（禁止运行时 Instantiate/Destroy），
    /// 顶部居中（TopBar 下方 120px）垂直堆叠，DOTween Sequence 控制生命周期。
    /// </summary>
    public class ToastManager : MonoBehaviour
    {
        private static ToastManager _instance;
        public static ToastManager Instance => _instance;

        [SerializeField] private GameObject toastPrefab;
        [SerializeField] private float topOffset = 120f;
        [SerializeField] private float spacing = 16f;

        private readonly Stack<GameObject> _pool = new Stack<GameObject>();
        private readonly List<GameObject> _active = new List<GameObject>();

        public int ActiveCount => _active.Count;

        /// <summary>运行时绑定 toast 预制件。</summary>
        public void SetPrefab(GameObject prefab) => toastPrefab = prefab;

        private void Awake()
        {
            _instance = this;
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        /// <summary>从池中取 toast 显示，完成后回收。</summary>
        public void Show(string text)
        {
            if (toastPrefab == null)
            {
                Debug.LogWarning("[UI] ToastManager 未绑定 toastPrefab");
                return;
            }

            GameObject go;
            if (_pool.Count > 0)
            {
                go = _pool.Pop();
                go.SetActive(true);
            }
            else
            {
                go = Instantiate(toastPrefab, transform);
            }

            var txt = go.GetComponentInChildren<Text>();
            if (txt != null) txt.text = text;

            go.transform.SetAsLastSibling();
            _active.Add(go);

            var cg = go.GetComponent<CanvasGroup>();
            if (cg == null) cg = go.AddComponent<CanvasGroup>();
            cg.alpha = 0f;

            // 初始定位：顶部居中向下堆叠
            go.transform.localPosition = GetStackPosition(_active.Count - 1);

            // 生命周期：淡入 → 停留 → 淡出 → 回收
            Sequence seq = DOTween.Sequence();
            seq.Append(cg.DOFade(1f, 0.15f))
               .AppendInterval(1.6f)
               .Append(cg.DOFade(0f, 0.25f))
               .OnComplete(() => Recycle(go))
               .SetLink(go);

            // 顶部堆叠：其余 toast 下移回位（新 toast 在最上）
            for (int i = 0; i < _active.Count - 1; i++)
            {
                var target = GetStackPosition(i);
                _active[i].transform.DOMoveY(target.y, 0.15f).SetEase(Ease.OutQuad).SetLink(_active[i]);
            }
        }

        private void Recycle(GameObject go)
        {
            _active.Remove(go);
            go.SetActive(false);
            _pool.Push(go);

            // 其余 toast 回位
            for (int i = 0; i < _active.Count; i++)
            {
                var target = GetStackPosition(i);
                _active[i].transform.DOMoveY(target.y, 0.15f).SetEase(Ease.OutQuad).SetLink(_active[i]);
            }
        }

        private Vector3 GetStackPosition(int index)
        {
            float y = topOffset + index * spacing;
            return new Vector3(0f, y, 0f);
        }
    }
}
