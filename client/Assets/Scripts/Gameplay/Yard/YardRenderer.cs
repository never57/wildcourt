using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WildCourt.Net.Protocol;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 庭院渲染（阶段 C，占位美术）：等轴测 45° 布局、按 (x+y) 遮挡排序、编辑幽灵预览、点选/长按移动。
    /// 美术资源到位后仅需替换 Sprite 来源。
    /// </summary>
    public class YardRenderer : MonoBehaviour
    {
        public static YardRenderer Instance { get; private set; }

        /// <summary>等轴测单元格半宽/半高（world 单位）。</summary>
        public const float IsoW = 1.0f;
        public const float IsoH = 0.5f;

        /// <summary>相机适配参数（20×20 庭院完整入画）。</summary>
        public const float CameraSize = 13f;
        public const float WorldCenterY = 4.75f;

        /// <summary>长按进入“移动/拖放”的阈值（秒）。</summary>
        public const float LongPressSeconds = 0.5f;

        /// <summary>缩放范围与步长（滚轮与面板 +/- 按钮共用）。</summary>
        public const float MinZoom = 5f;
        public const float MaxZoom = 20f;
        public const float ZoomStep = 1.5f;

        /// <summary>选中/取消选中事件（YardPanel 订阅显示操作按钮）。</summary>
        public event Action<int> ItemSelected;
        public event Action ItemDeselected;

        private static readonly Color GrassColor = new Color(0.72f, 0.88f, 0.62f);
        private static readonly Color WallColor = new Color(0.55f, 0.42f, 0.28f);
        private static readonly Color GhostValidColor = new Color(0.3f, 0.9f, 0.3f, 0.7f);
        private static readonly Color GhostInvalidColor = new Color(0.9f, 0.3f, 0.3f, 0.7f);

        private static readonly Dictionary<string, Color> CategoryColors = new()
        {
            ["景观"] = new Color(0.35f, 0.75f, 0.35f),
            ["家具"] = new Color(0.72f, 0.55f, 0.35f),
            ["容器"] = new Color(0.55f, 0.7f, 0.75f),
            ["建筑小品"] = new Color(0.65f, 0.65f, 0.7f),
            ["展示架"] = new Color(0.85f, 0.7f, 0.3f),
            ["宠物用品"] = new Color(0.9f, 0.6f, 0.75f),
            ["地面装饰"] = new Color(0.6f, 0.8f, 0.5f),
            ["节日限定"] = new Color(0.9f, 0.45f, 0.35f),
        };

        private readonly List<GameObject> _placed = new();
        private GameObject _ground;
        private GameObject _ghost;
        private SpriteRenderer _ghostRenderer;
        private Sprite _solidSprite;
        private Sprite _diamondSprite;
        private int _selectedIndex = -1;
        private int _moveIndex = -1;
        private bool _clickPending;
        private float _downTime;
        private float _placingStartedAt;
        private YardManager.EditState _lastState;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
        }

        private void OnEnable()
        {
            var yard = YardManager.Instance;
            if (yard != null)
            {
                yard.YardLoaded += OnYardLoaded;
                yard.LayoutChanged += OnLayoutChanged;
                yard.Error += OnYardError;
            }
            _solidSprite = CreateSolidSprite(Color.white, 4);
            _diamondSprite = CreateDiamondSprite(Color.white, 16);
            BuildGround();
            Rebuild();
        }

        private void OnDisable()
        {
            var yard = YardManager.Instance;
            if (yard != null)
            {
                yard.YardLoaded -= OnYardLoaded;
                yard.LayoutChanged -= OnLayoutChanged;
                yard.Error -= OnYardError;
            }
        }

        private void Update()
        {
            HandleZoom();

            var yard = YardManager.Instance;
            if (yard == null) return;

            if (yard.State == YardManager.EditState.Placing)
            {
                if (_lastState != YardManager.EditState.Placing)
                {
                    _placingStartedAt = Time.time;
                }
                _lastState = yard.State;
                UpdatePlaceGhost(yard);
                return;
            }

            _lastState = yard.State;
            UpdateSelectAndMove(yard);
        }

        // ---- 放置幽灵 ----

        private void UpdatePlaceGhost(YardManager yard)
        {
            var (x, y) = ScreenToCell(Input.mousePosition);
            bool valid = yard.CanPlaceAt(x, y);
            ShowGhost(yard.SelectedDecoration, x, y, 0, false, valid);

            if (Input.GetMouseButtonUp(0))
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (!overUi && TryPlaceAtPointer())
                {
                    // 放置成功：TryPlace 内部已将状态切回 Idle，由面板重新选择下一件装饰
                }
                else if (Time.time - _placingStartedAt > 0.1f)
                {
                    // 松手在面板上且不是“点选刚进入放置”的同一帧 → 取消
                    yard.ExitEditMode();
                }
                HideGhost();
            }
            else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
            {
                yard.ExitEditMode();
                HideGhost();
            }
        }

        /// <summary>在指针所在格尝试放置（面板长按拖放松手与点击共用）。</summary>
        public bool TryPlaceAtPointer()
        {
            var yard = YardManager.Instance;
            if (yard == null) return false;
            var (x, y) = ScreenToCell(Input.mousePosition);
            return yard.TryPlace(x, y);
        }

        // ---- 点选 / 长按移动 ----

        private void UpdateSelectAndMove(YardManager yard)
        {
            if (Input.GetMouseButtonDown(0))
            {
                _clickPending = true;
                _downTime = Time.time;
            }

            if (_clickPending && Input.GetMouseButton(0) && Time.time - _downTime >= LongPressSeconds)
            {
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                _clickPending = false;
                if (!overUi)
                {
                    int idx = FindItemAtScreen(Input.mousePosition);
                    if (idx >= 0)
                    {
                        _moveIndex = idx;
                        var item = yard.Items[idx];
                        ShowGhost(GetDecoration(item.id), item.x, item.y, item.rotation, item.mirror, true);
                    }
                }
            }

            if (_moveIndex >= 0)
            {
                var item = yard.Items[_moveIndex];
                var (x, y) = ScreenToCell(Input.mousePosition);
                bool valid = yard.CanMoveTo(_moveIndex, x, y);
                ShowGhost(GetDecoration(item.id), x, y, item.rotation, item.mirror, valid);
                if (Input.GetMouseButtonUp(0))
                {
                    bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                    if (valid && !overUi) yard.MoveItem(_moveIndex, x, y);
                    _moveIndex = -1;
                    HideGhost();
                }
                else if (Input.GetMouseButtonDown(1) || Input.GetKeyDown(KeyCode.Escape))
                {
                    _moveIndex = -1;
                    HideGhost();
                }
                return;
            }

            if (_clickPending && Input.GetMouseButtonUp(0))
            {
                _clickPending = false;
                bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
                if (overUi) return;
                int idx = FindItemAtScreen(Input.mousePosition);
                if (idx >= 0) SelectItem(idx);
                else DeselectItem();
            }
        }

        private void SelectItem(int index)
        {
            _selectedIndex = index;
            ItemSelected?.Invoke(index);
        }

        private void DeselectItem()
        {
            if (_selectedIndex < 0) return;
            _selectedIndex = -1;
            ItemDeselected?.Invoke();
        }

        // ---- 事件 ----

        private void OnYardLoaded()
        {
            DeselectItem();
            Rebuild();
        }

        private void OnLayoutChanged()
        {
            DeselectItem();
            Rebuild();
        }

        private void OnYardError(string msg)
        {
            Debug.LogWarning($"[YardRenderer] {msg}");
        }

        // ---- 渲染 ----

        private void Rebuild()
        {
            foreach (var go in _placed)
            {
                if (go != null) Destroy(go);
            }
            _placed.Clear();

            var yard = YardManager.Instance;
            if (yard == null) return;

            for (int i = 0; i < yard.Items.Count; i++)
            {
                var item = yard.Items[i];
                var d = GetDecoration(item.id);
                if (d == null) continue;
                var go = CreateCellSprite(d, item.x, item.y, item.rotation, item.mirror);
                _placed.Add(go);
            }
        }

        private GameObject CreateCellSprite(DecorationConfig d, int x, int y, int rotation, bool mirror)
        {
            var go = new GameObject("deco_" + d.id);
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _diamondSprite;
            sr.color = GetCategoryColor(d.category);
            sr.sortingOrder = (x + y) * 10;
            var (w, h) = GetSize(d, rotation);
            go.transform.localPosition = IsoToWorld(x, y);

            // 网格单元规格 = 宽 1、高 0.5（2:1 菱形）：占位按 (w, h×0.5)；真实图等比适配进该矩形，不拉伸
            float cellH = h * 0.5f;
            float baseScaleX = w * (mirror ? -1f : 1f);
            go.transform.localScale = new Vector3(baseScaleX, cellH, 1f);

            // 商品图异步加载（StreamingAssets/ui/Decorations/{id}.png）：成功后换图并按原图比例适配
            GoodsSprites.Request(d.id, (loadedId, sprite) =>
            {
                if (sprite == null || sr == null) return;
                var bounds = sprite.bounds.size;
                float aspect = bounds.x / Mathf.Max(bounds.y, 0.0001f);
                float sx = baseScaleX;
                float sy = cellH;
                if (Mathf.Abs(baseScaleX) / cellH > aspect)
                {
                    sx = cellH * aspect * (mirror ? -1f : 1f);
                }
                else
                {
                    sy = Mathf.Abs(baseScaleX) / aspect;
                }
                sr.sprite = sprite;
                sr.color = Color.white;
                sr.transform.localScale = new Vector3(sx, sy, 1f);
            });

            if (!string.IsNullOrEmpty(d.effect))
            {
                CreateEffectParticles(go, d.effect, x, y);
            }
            return go;
        }

        /// <summary>占位粒子（如树类的飘落效果），后续按美术配置调参。</summary>
        private static void CreateEffectParticles(GameObject parent, string effect, int x, int y)
        {
            var go = new GameObject("fx_" + effect);
            go.transform.SetParent(parent.transform, false);
            go.transform.localPosition = new Vector3(0f, 0.45f, 0f);

            var ps = go.AddComponent<ParticleSystem>();
            var main = ps.main;
            main.loop = true;
            main.startLifetime = 2.5f;
            main.startSpeed = 0.35f;
            main.startSize = 0.1f;
            main.gravityModifier = 0.12f;
            main.startColor = new Color(0.45f, 0.85f, 0.35f, 0.85f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            var emission = ps.emission;
            emission.rateOverTime = 5f;
            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(0.9f, 0.2f, 0.1f);

            var renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Billboard;
            var mat = new Material(Shader.Find("Sprites/Default"));
            if (mat != null) renderer.material = mat;
            renderer.sortingOrder = (x + y) * 10 + 1;
        }

        private void BuildGround()
        {
            if (_ground != null) Destroy(_ground);

            _ground = new GameObject("yard_ground");
            _ground.transform.SetParent(transform, false);
            var sr = _ground.AddComponent<SpriteRenderer>();
            sr.sprite = _diamondSprite;
            sr.color = GrassColor;
            sr.sortingOrder = -1000;
            _ground.transform.localPosition = new Vector3(0f, WorldCenterY, 0f);
            _ground.transform.localScale = new Vector3(21f, 10.5f, 1f);

            for (int x = 0; x < YardManager.YardWidth; x++)
            {
                SpawnWall(x, 0);
                SpawnWall(x, YardManager.YardHeight - 1);
            }
            for (int y = 0; y < YardManager.YardHeight; y++)
            {
                SpawnWall(0, y);
                SpawnWall(YardManager.YardWidth - 1, y);
            }
        }

        private void SpawnWall(int x, int y)
        {
            var go = new GameObject("wall");
            go.transform.SetParent(transform, false);
            var sr = go.AddComponent<SpriteRenderer>();
            sr.sprite = _diamondSprite;
            sr.color = WallColor;
            sr.sortingOrder = (x + y) * 10 - 5;
            go.transform.localPosition = IsoToWorld(x, y);
            go.transform.localScale = new Vector3(1f, 0.5f, 1f);
        }

        // ---- 幽灵 ----

        private void ShowGhost(DecorationConfig d, int x, int y, int rotation, bool mirror, bool valid)
        {
            if (d == null)
            {
                HideGhost();
                return;
            }
            if (_ghost == null)
            {
                _ghost = new GameObject("ghost");
                _ghost.transform.SetParent(transform, false);
                _ghostRenderer = _ghost.AddComponent<SpriteRenderer>();
                _ghostRenderer.sprite = _diamondSprite;
                _ghostRenderer.sortingOrder = 9999;
            }
            var (w, h) = GetSize(d, rotation);
            _ghostRenderer.color = valid ? GhostValidColor : GhostInvalidColor;
            _ghost.transform.localPosition = IsoToWorld(x, y);
            _ghost.transform.localScale = new Vector3(w * (mirror ? -1f : 1f), h * 0.5f, 1f);
        }

        private void HideGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderer = null;
        }

        // ---- 缩放 ----

        private void HandleZoom()
        {
            float scroll = Input.mouseScrollDelta.y;
            if (Mathf.Abs(scroll) > 0.01f)
            {
                ZoomBy(scroll * ZoomStep);
            }
        }

        /// <summary>缩放（+ 放大 / - 缩小），钳制 MinZoom~MaxZoom；面板 +/- 按钮调用。</summary>
        public void ZoomBy(float delta)
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographicSize = Mathf.Clamp(cam.orthographicSize - delta, MinZoom, MaxZoom);
        }

        // ---- 工具 ----

        public (int X, int Y) ScreenToCell(Vector3 screen)
        {
            var wp = ScreenToWorld(screen);
            float a = IsoW * 0.5f;
            float b = IsoH * 0.5f;
            float gx = (wp.x / a + wp.y / b) * 0.5f;
            float gy = (wp.y / b - wp.x / a) * 0.5f;
            return (Mathf.FloorToInt(gx), Mathf.FloorToInt(gy));
        }

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        }

        public static Vector3 IsoToWorld(int x, int y)
        {
            return new Vector3((x - y) * IsoW * 0.5f, (x + y) * IsoH * 0.5f, 0f);
        }

        /// <summary>
        /// 按“视觉块”命中：以锚点为中心的轴对齐矩形（半宽/半高按配置与旋转，镜像不改变矩形），
        /// 点击物块任意位置都可命中；重叠时取 x+y 最大的（近下远上）。
        /// </summary>
        private int FindItemAtScreen(Vector3 screen)
        {
            var yard = YardManager.Instance;
            if (yard == null) return -1;
            var wp = ScreenToWorld(screen);
            int best = -1;
            int bestSum = int.MinValue;
            for (int i = 0; i < yard.Items.Count; i++)
            {
                var item = yard.Items[i];
                var d = GetDecoration(item.id);
                if (d == null) continue;
                var (w, h) = GetSize(d, item.rotation);
                var center = IsoToWorld(item.x, item.y);
                if (Mathf.Abs(wp.x - center.x) <= w * 0.5f &&
                    Mathf.Abs(wp.y - center.y) <= h * 0.25f &&
                    item.x + item.y > bestSum)
                {
                    best = i;
                    bestSum = item.x + item.y;
                }
            }
            return best;
        }

        private DecorationConfig GetDecoration(string id)
        {
            return YardManager.Instance != null ? YardManager.Instance.GetDecoration(id) : null;
        }

        private static (int W, int H) GetSize(DecorationConfig d, int rotation)
        {
            bool swap = d.sizeX != d.sizeY && (rotation == 90 || rotation == 270);
            return swap ? (d.sizeY, d.sizeX) : (d.sizeX, d.sizeY);
        }

        public static Color GetCategoryColor(string category)
        {
            return CategoryColors.TryGetValue(category ?? string.Empty, out var c) ? c : Color.gray;
        }

        private static Sprite CreateSolidSprite(Color color, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            var pixels = new Color[size * size];
            for (int i = 0; i < pixels.Length; i++) pixels[i] = color;
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }

        /// <summary>生成菱形贴图（等轴测占位，|dx|+|dy| ≤ r 填充），scale 按 w×h 即得对角线 w×h 的菱形。</summary>
        private static Sprite CreateDiamondSprite(Color color, int size)
        {
            var tex = new Texture2D(size, size, TextureFormat.RGBA32, false);
            float center = (size - 1) * 0.5f;
            float radius = size * 0.5f;
            var pixels = new Color[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = Mathf.Abs(x - center);
                    float dy = Mathf.Abs(y - center);
                    pixels[y * size + x] = dx + dy <= radius ? color : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}
