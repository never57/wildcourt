using UnityEngine;
using UnityEngine.Tilemaps;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 场景瓦片地图标记（提前摆放）：挂到庭院地面的 Grid 物体上（Grid + Tilemap 由美术在场景里摆好）。
    /// YardRenderer 检测到它后，不再动态生成地面/围墙，并改用该 Grid 做逻辑坐标与世界坐标互转。
    /// 约定：逻辑坐标 (0,0) == Grid cell (0,0) + origin；cellSize 1×1、俯视方格（Rectangle 布局）。
    /// </summary>
    [DisallowMultipleComponent]
    public class YardTilemap : MonoBehaviour
    {
        [Tooltip("自动获取自身 Grid；也可手动指定")]
        public Grid grid;

        [Tooltip("自动获取自身 Tilemap；也可手动指定。用于计算已铺瓦片的实际范围中心。")]
        public Tilemap tilemap;

        [Tooltip("场景 Grid cell 的起点偏移：逻辑 (0,0) 对应 cell (0,0)+origin。美术从 cell(-2,-11) 开始铺时填 (-2,-11,0)。")]
        public Vector3Int origin = Vector3Int.zero;

        private void Reset()
        {
            if (grid == null) grid = GetComponent<Grid>();
            if (tilemap == null) tilemap = GetComponentInChildren<Tilemap>();
        }

        private void Awake()
        {
            if (grid == null) grid = GetComponent<Grid>();
            if (tilemap == null) tilemap = GetComponentInChildren<Tilemap>();
        }

        /// <summary>
        /// 已铺瓦片区域的世界中心：相机对准它即可让实际地面居中。
        /// 无 Tilemap（仅普通 Grid）时回退到逻辑庭院中心。
        /// </summary>
        public Vector3 GetGroundCenterWorld()
        {
            if (grid != null && tilemap != null)
            {
                var bounds = tilemap.cellBounds;
                if (bounds.size.x > 0 && bounds.size.y > 0)
                {
                    var center = new Vector3Int(bounds.xMin + bounds.size.x / 2, bounds.yMin + bounds.size.y / 2, 0);
                    return grid.GetCellCenterWorld(center);
                }
            }
            return CellToWorld(YardManager.YardWidth / 2, YardManager.YardHeight / 2);
        }

        /// <summary>逻辑坐标 (x,y) → 世界坐标（格子中心）。无 Grid 时按左下角 (0,0)、边长 1 的方格回退。</summary>
        public Vector3 CellToWorld(int x, int y)
        {
            if (grid != null)
            {
                return grid.GetCellCenterWorld(new Vector3Int(x, y, 0) + origin);
            }
            return new Vector3(x + 0.5f, y + 0.5f, 0f);
        }

        /// <summary>世界坐标 → 逻辑坐标。</summary>
        public (int X, int Y) WorldToCell(Vector3 world)
        {
            if (grid != null)
            {
                var c = grid.WorldToCell(world);
                return (c.x - origin.x, c.y - origin.y);
            }
            return (Mathf.FloorToInt(world.x), Mathf.FloorToInt(world.y));
        }
    }
}
