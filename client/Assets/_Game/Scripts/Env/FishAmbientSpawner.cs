using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Game.Env
{
    /// <summary>
    /// 鱼群环境生成器：对象池生成 fishCount 条游动鱼（PF_FishSwim），挂在 FishLayer 上。
    /// 运行时仅启用/回收池对象，禁止即时 Instantiate/Destroy。
    /// </summary>
    public class FishAmbientSpawner : MonoBehaviour
    {
        [SerializeField] GameObject fishPrefab;   // PF_FishSwim
        [SerializeField] int fishCount = 8;       // 6~10
        [SerializeField] Sprite[] fishSprites;    // 5 种占位鱼图，随机取

        private readonly List<FishSwimController> _pool = new List<FishSwimController>();
        private readonly List<FishSwimController> _active = new List<FishSwimController>();
        private System.Random _rng;

        public int ActiveCount => _active.Count;
        public IReadOnlyList<FishSwimController> Active => _active;

        private void Awake()
        {
            _rng = new System.Random(GetInstanceID());
        }

        private void Start()
        {
            int n = Mathf.Clamp(fishCount, 6, 10);
            if (fishPrefab == null)
            {
                Debug.LogWarning("[Env] FishAmbientSpawner 未绑定 fishPrefab，跳过生成");
                return;
            }

            for (int i = 0; i < n; i++)
                SpawnOne();
        }

        private void SpawnOne()
        {
            FishSwimController ctrl;
            if (_pool.Count > 0)
            {
                ctrl = _pool[_pool.Count - 1];
                _pool.RemoveAt(_pool.Count - 1);
            }
            else
            {
                var go = Instantiate(fishPrefab, transform);
                ctrl = go.GetComponent<FishSwimController>();
                if (ctrl == null) ctrl = go.AddComponent<FishSwimController>();
            }

            // 随机深度/层序
            ctrl.transform.SetAsLastSibling();

            Sprite sp = null;
            if (fishSprites != null && fishSprites.Length > 0)
                sp = fishSprites[_rng.Next(fishSprites.Length)];

            float scale = 0.6f + (float)_rng.NextDouble() * 0.8f;         // 0.6~1.4
            byte alpha = (byte)(150 + _rng.Next(106));                    // 150~255
            ctrl.Initialize(sp, scale, alpha);

            // 随机锚点位置（FishLayer 矩形内，四周留边）
            var rt = (RectTransform)ctrl.transform;
            var parent = (RectTransform)transform;
            float marginX = 40f, marginY = 30f;
            float x = (float)_rng.NextDouble() * (parent.rect.width - marginX * 2f) + parent.rect.xMin + marginX;
            float y = (float)_rng.NextDouble() * (parent.rect.height - marginY * 2f) + parent.rect.yMin + marginY;
            rt.anchoredPosition = new Vector2(x, y);
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);

            ctrl.gameObject.SetActive(true);
            _active.Add(ctrl);
        }

        private void OnDestroy()
        {
            foreach (var c in _active) _pool.Add(c);
            _active.Clear();
        }
    }
}
