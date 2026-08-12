using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using WildCourt.Net.Protocol;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 庭院渲染：俯视方格布局。地面优先使用场景里提前摆好的瓦片地图（YardTilemap），
    /// 检测不到时回退到代码动态生成占位地面/围墙；装饰物仍为运行时代码实例化。
    /// </summary>
    public class YardRenderer : MonoBehaviour
    {
        public static YardRenderer Instance { get; private set; }

        /// <summary>俯视方格单元格边长（world 单位，与场景 Grid.cellSize 保持一致）。</summary>
        public const float CellSize = 1f;

        /// <summary>相机初始缩放（最远视角；配合 MaxZoom 使用，初始即上限，只允许往里缩）。</summary>
        public const float CameraSize = 5f;

        /// <summary>长按进入“移动/拖放”的阈值（秒）。</summary>
        public const float LongPressSeconds = 0.2f;

        /// <summary>缩放范围与步长（滚轮与面板 +/- 按钮共用）。
        /// MaxZoom 与 CameraSize 保持一致：初始即最远视角，只允许往内缩进，不允许再往外退。</summary>
        public const float MinZoom = 1f;
        public const float MaxZoom = 5f;
        public const float ZoomStep = 0.5f;

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
        private YardTilemap _yardTilemap;
        private Grid _grid;
        private Vector3Int _origin;
        private GameObject _ground;
        private GameObject _ghost;
        private SpriteRenderer _ghostRenderer;
        private string _ghostId;
        private Sprite _ghostSprite;
        private int _ghostW, _ghostH;
        private bool _ghostMirror;
        private float _ghostScale = 1f;
        private Sprite _solidSprite;
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
            _yardTilemap = FindObjectOfType<YardTilemap>();
            if (_yardTilemap != null)
            {
                _grid = _yardTilemap.grid;
                _origin = _yardTilemap.origin;
            }
            else
            {
                _grid = FindObjectOfType<Grid>();
                _origin = Vector3Int.zero;
            }
            // 放置边界跟随瓦片地图实际范围：瓦片没铺满 20×20 时，禁止把装饰放到没有地面的区域。
            if (yard != null && _yardTilemap != null)
            {
                var (xMin, yMin, w, h) = _yardTilemap.GetGroundBoundsLogic();
                yard.PlaceXMin = xMin;
                yard.PlaceYMin = yMin;
                yard.PlaceXMax = xMin + w - 1;
                yard.PlaceYMax = yMin + h - 1;
            }
            _solidSprite = CreateSolidSprite(Color.white, 4);
            AlignCameraToGrid();
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

        /// <summary>退出建造模式：取消选中、清除放置/移动 ghost。</summary>
        public void CancelSelection()
        {
            _moveIndex = -1;
            _clickPending = false;
            HideGhost();
            DeselectItem();
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
            for (int i = 0; i < _placed.Count; i++)
            {
                var go = _placed[i];
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
            sr.sprite = _solidSprite;
            sr.color = GetCategoryColor(d.category);
            sr.sortingOrder = GetSortingOrder(x, y);
            var (w, h) = GetSize(d, rotation);
            go.transform.localPosition = CellToWorld(x, y);

            // 俯视方格：网格单元 = 1×1；占位按 (w, h) 满格；真实图等比适配进该矩形，不拉伸。
            // displayScale 仅缩放显示，占格与碰撞仍按 (w, h) 计算。
            float s = d.displayScale;
            float cellH = h * CellSize * s;
            float baseScaleX = w * CellSize * s * (mirror ? -1f : 1f);
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
            renderer.sortingOrder = GetSortingOrder(x, y) + 1;
        }

        private void BuildGround()
        {
            // 场景预置地面（Grid 瓦片地图 / YardGround 组件）时不再动态生成地面/围墙，尊重“提前摆放”
            if (_grid != null) return;
            if (FindObjectOfType<YardGround>() != null) return;

            if (_ground != null) Destroy(_ground);

            _ground = new GameObject("yard_ground");
            _ground.transform.SetParent(transform, false);
            var sr = _ground.AddComponent<SpriteRenderer>();
            sr.sprite = _solidSprite;
            sr.color = GrassColor;
            sr.sortingOrder = -1000;
            var center = CellToWorld(YardManager.YardWidth * 0.5f, YardManager.YardHeight * 0.5f);
            _ground.transform.localPosition = center;
            _ground.transform.localScale = new Vector3(YardManager.YardWidth * CellSize, YardManager.YardHeight * CellSize, 1f);

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
            sr.sprite = _solidSprite;
            sr.color = WallColor;
            sr.sortingOrder = GetSortingOrder(x, y) - 5;
            go.transform.localPosition = CellToWorld(x, y);
            go.transform.localScale = new Vector3(CellSize, CellSize, 1f);
        }

        // ---- 幽灵 ----

        /// <summary>
        /// 移动/放置预览（幽灵）：
        /// 显示装饰物真实图片形状（异步加载，无图回退矩形占位）；
        /// 合法性仍由调用方按占格矩形判断，这里只把 valid 反映到颜色上。
        /// </summary>
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
                _ghostRenderer.sprite = _solidSprite;
                _ghostRenderer.sortingOrder = 9999;
            }
            // 重置为矩形占位，等待真实图回调（避免切换物品时残留上一物品的图）
            _ghostSprite = null;
            _ghostRenderer.sprite = _solidSprite;
            _ghostId = d.id;
            var (w, h) = GetSize(d, rotation);
            _ghostW = w;
            _ghostH = h;
            _ghostMirror = mirror;
            _ghostScale = d.displayScale;
            _ghostRenderer.color = valid ? GhostValidColor : GhostInvalidColor;
            _ghost.transform.localPosition = CellToWorld(x, y);
            ApplyGhostScale();

            GoodsSprites.Request(d.id, (loadedId, sprite) =>
            {
                if (sprite == null || _ghostRenderer == null || loadedId != _ghostId) return;
                _ghostSprite = sprite;
                _ghostRenderer.sprite = sprite;
                ApplyGhostScale();
            });
        }

        /// <summary>幽灵缩放：有真实图按原图比例适配进占格矩形；无图用矩形占位。displayScale 只影响显示。</summary>
        private void ApplyGhostScale()
        {
            if (_ghostRenderer == null) return;
            float cellH = _ghostH * CellSize * _ghostScale;
            float baseScaleX = _ghostW * CellSize * _ghostScale * (_ghostMirror ? -1f : 1f);
            if (_ghostSprite != null)
            {
                var bounds = _ghostSprite.bounds.size;
                float aspect = bounds.x / Mathf.Max(bounds.y, 0.0001f);
                float sx = baseScaleX;
                float sy = cellH;
                if (Mathf.Abs(baseScaleX) / cellH > aspect)
                {
                    sx = cellH * aspect * (_ghostMirror ? -1f : 1f);
                }
                else
                {
                    sy = Mathf.Abs(baseScaleX) / aspect;
                }
                _ghostRenderer.transform.localScale = new Vector3(sx, sy, 1f);
            }
            else
            {
                _ghostRenderer.transform.localScale = new Vector3(baseScaleX, cellH, 1f);
            }
        }

        private void HideGhost()
        {
            if (_ghost != null) Destroy(_ghost);
            _ghost = null;
            _ghostRenderer = null;
            _ghostSprite = null;
            _ghostId = null;
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

        /// <summary>
        /// 初始化相机：正交视角 + 最远初始缩放（看全庭院）。
        /// 相机位置尊重场景里设置的初始位置（MainScene 主相机），不再自动对准 Grid；
        /// 之后可通过滚轮 / 面板按钮缩小 orthographicSize 往内缩进。
        /// </summary>
        private void AlignCameraToGrid()
        {
            var cam = Camera.main;
            if (cam == null) return;
            cam.orthographicSize = CameraSize;
        }

        // ---- 工具 ----

        public (int X, int Y) ScreenToCell(Vector3 screen)
        {
            var wp = ScreenToWorld(screen);
            if (_grid != null)
            {
                var cell = _grid.WorldToCell(wp) - _origin;
                return (cell.x, cell.y);
            }
            return (Mathf.FloorToInt(wp.x / CellSize), Mathf.FloorToInt(wp.y / CellSize));
        }

        public Vector3 ScreenToWorld(Vector3 screen)
        {
            var cam = Camera.main;
            if (cam == null) return Vector3.zero;
            return cam.ScreenToWorldPoint(new Vector3(screen.x, screen.y, 10f));
        }

        public Vector3 CellToWorld(float x, float y)
        {
            if (_grid != null)
            {
                return _grid.GetCellCenterWorld(new Vector3Int((int)x, (int)y, 0) + _origin);
            }
            return new Vector3(x * CellSize, y * CellSize, 0f);
        }

        /// <summary>
        /// 俯视方格遮挡排序：y 越大（越靠屏幕上方）越靠后渲染。
        /// 基础偏移 1000 保证装饰物永远高于场景瓦片地图（TilemapRenderer 默认排序 0），
        /// 避免树等物块被地面瓦片盖住。
        /// </summary>
        private static int GetSortingOrder(int x, int y)
        {
            return 1000 - y * 10;
        }

        /// <summary>
        /// 按“视觉块”命中：以锚点为中心的轴对齐矩形（半宽/半高按配置与旋转，镜像不改变矩形），
        /// 点击物块任意位置都可命中；重叠时取 y 最小的（屏幕下方、视觉靠前）。
        /// </summary>
        private int FindItemAtScreen(Vector3 screen)
        {
            var yard = YardManager.Instance;
            if (yard == null) return -1;
            var wp = ScreenToWorld(screen);
            int best = -1;
            int bestY = int.MaxValue;
            for (int i = 0; i < yard.Items.Count; i++)
            {
                var item = yard.Items[i];
                var d = GetDecoration(item.id);
                if (d == null) continue;
                var (w, h) = GetSize(d, item.rotation);
                var center = CellToWorld(item.x, item.y);
                if (Mathf.Abs(wp.x - center.x) <= w * 0.5f &&
                    Mathf.Abs(wp.y - center.y) <= h * 0.5f &&
                    item.y < bestY)
                {
                    best = i;
                    bestY = item.y;
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

    }
}
