using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Game.EditorTools
{
    /// <summary>
    /// 钓鱼系统占位美术生成工具。
    /// 程序化生成纯色图标 / 鱼形剪影 / 鱼竿占位 PNG，并设置导入格式为 Sprite (2D and UI)。
    /// </summary>
    public static class PlaceholderArtGenerator
    {
        private const string PlaceholderDir = "Assets/_Game/Art/Placeholder";

        // 图标尺寸、文件名、颜色
        private static readonly Dictionary<string, (int w, int h, Color c)> Icons = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Coin", (64, 64, new Color(1.00f, 0.84f, 0.20f, 1f)) },                 // 金黄
            { "PH_Bait", (64, 64, new Color(0.72f, 0.53f, 0.24f, 1f)) },                 // 土黄
        };

        // 鱼形剪影（256x128，椭圆身体 + 三角尾巴，朝右）
        private static readonly Dictionary<string, (int w, int h, Color c)> Fish = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Fish_Crucian",  (256, 128, new Color(0.75f, 0.75f, 0.78f, 1f)) },     // 银灰
            { "PH_Fish_Carp",     (256, 128, new Color(0.95f, 0.48f, 0.20f, 1f)) },     // 橙红
            { "PH_Fish_Goldfish", (256, 128, new Color(0.95f, 0.72f, 0.20f, 1f)) },     // 金色
            { "PH_Fish_GrassCarp",(256, 128, new Color(0.55f, 0.72f, 0.68f, 1f)) },     // 青灰
            { "PH_Fish_Bass",     (256, 128, new Color(0.25f, 0.45f, 0.75f, 1f)) },     // 海蓝
        };

        // 鱼竿占位（256x64 棕色长条）
        private static readonly Dictionary<string, (int w, int h, Color c)> Rods = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Rod", (256, 64, new Color(0.55f, 0.40f, 0.25f, 1f)) },               // 棕色
        };

        // 垂直渐变背景（512x512，顶→底）
        private static readonly Dictionary<string, (int w, int h, Color top, Color bottom)> Gradients = new Dictionary<string, (int, int, Color, Color)>
        {
            { "PH_Sky_Sunset", (512, 512, new Color(0.969f, 0.784f, 0.451f, 1f), new Color(0.941f, 0.627f, 0.353f, 1f)) }, // #F7C873 → #F0A05A
            { "PH_Water",      (512, 512, new Color(0.910f, 0.831f, 0.541f, 1f), new Color(0.498f, 0.722f, 0.643f, 1f)) }, // #E8D48A → #7FB8A4
        };

        // UI 圆形（按钮底 / 功能图标）
        private static readonly Dictionary<string, (int w, int h, Color c)> UiCircles = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Circle_White", (96, 96, new Color(1f, 1f, 1f, 0.95f)) },                            // 白色圆形按钮底
            { "PH_Icon_Avatar",  (64, 64, new Color(0.96f, 0.82f, 0.69f, 1f)) },                     // 肤色
            { "PH_Icon_News",    (64, 64, new Color(0.98f, 0.94f, 0.78f, 1f)) },                     // 米黄
            { "PH_Icon_Codex",   (64, 64, new Color(0.75f, 0.87f, 0.96f, 1f)) },                     // 浅蓝
            { "PH_Icon_Bag",     (64, 64, new Color(0.78f, 0.92f, 0.76f, 1f)) },                     // 浅绿
            { "PH_Icon_Spot",    (64, 64, new Color(0.86f, 0.77f, 0.63f, 1f)) },                     // 浅棕
            { "PH_Icon_Shop",    (64, 64, new Color(0.98f, 0.65f, 0.35f, 1f)) },                     // 橙
            { "PH_Icon_Creel",   (64, 64, new Color(0.80f, 0.80f, 0.82f, 1f)) },                     // 浅灰
        };

        // 圆角矩形面板 / 气泡底（alpha 240）
        private static readonly Dictionary<string, (int w, int h, Color c)> RoundedPanels = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Panel_Rounded", (256, 128, new Color(1f, 1f, 1f, 240f / 255f)) },
        };

        // 加号图标
        private static readonly Dictionary<string, (int w, int h, Color c)> PlusIcons = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Icon_Plus", (48, 48, new Color(0.60f, 0.60f, 0.60f, 1f)) },                        // 灰色 ＋
        };

        // 鱼线（8x8 纯白）
        private static readonly Dictionary<string, (int w, int h, Color c)> LineWhite = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Line_White", (8, 8, new Color(1f, 1f, 1f, 1f)) },
        };

        // 浮漂（48x24 白色椭圆）
        private static readonly Dictionary<string, (int w, int h, Color c)> FloatElipses = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Float", (48, 24, new Color(0.95f, 0.96f, 0.98f, 1f)) },
        };

        // 进度条（256x32 圆角条）
        private static readonly Dictionary<string, (int w, int h, Color c)> Bars = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Bar_BG",   (256, 32, new Color(0.22f, 0.22f, 0.24f, 0.90f)) },   // 深灰底
            { "PH_Bar_Fill", (256, 32, new Color(0.36f, 0.78f, 0.42f, 1f)) },      // 绿色填充
        };

        // 背包 Tab（128x64 圆角）
        private static readonly Dictionary<string, (int w, int h, Color c)> Tabs = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Tab_Normal",   (128, 64, new Color(0.85f, 0.85f, 0.87f, 1f)) },   // 浅灰
            { "PH_Tab_Selected", (128, 64, new Color(0.62f, 0.84f, 0.62f, 1f)) },   // 浅绿
        };

        // 物品框（112x112 方框，正常灰边 / 稀有金边）
        private static readonly Dictionary<string, (int w, int h, Color c)> Frames = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Frame_Normal", (112, 112, new Color(0.60f, 0.62f, 0.65f, 1f)) },  // 灰边
            { "PH_Frame_Rare",   (112, 112, new Color(0.95f, 0.78f, 0.20f, 1f)) },  // 金边
        };

        // 商店：减号图标 / 购买按钮 / 数量快捷钮
        private static readonly Dictionary<string, (int w, int h, Color c)> ShopMinus = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Icon_Minus", (48, 48, new Color(0.60f, 0.60f, 0.60f, 1f)) },      // 灰色 －
        };

        private static readonly Dictionary<string, (int w, int h, Color c)> ShopBuyBtn = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Btn_Buy", (128, 64, new Color(0.45f, 0.82f, 0.45f, 1f)) },        // 绿色圆角
        };

        private static readonly Dictionary<string, (int w, int h, Color c)> ShopQtyBtn = new Dictionary<string, (int, int, Color)>
        {
            { "PH_Btn_Qty", (64, 48, new Color(0.86f, 0.86f, 0.88f, 1f)) },         // 浅灰圆角
        };

        [MenuItem("Tools/Generate Placeholder Art")]
        public static void GenerateAll()
        {
            if (!AssetDatabase.IsValidFolder(PlaceholderDir))
            {
                string parent = "Assets/_Game/Art";
                string leaf = "Placeholder";
                if (!AssetDatabase.IsValidFolder(parent))
                {
                    AssetDatabase.CreateFolder("Assets/_Game", "Art");
                }
                AssetDatabase.CreateFolder(parent, leaf);
            }

            int generated = 0;
            foreach (var kv in Icons)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillSolid)) generated++;
            }
            foreach (var kv in Fish)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillFish)) generated++;
            }
            foreach (var kv in Rods)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRod)) generated++;
            }
            foreach (var kv in Gradients)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.top, null, kv.Value.bottom)) generated++;
            }
            foreach (var kv in UiCircles)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillCircle)) generated++;
            }
            foreach (var kv in RoundedPanels)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRoundedRect)) generated++;
            }
            foreach (var kv in PlusIcons)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillPlus)) generated++;
            }
            foreach (var kv in LineWhite)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillSolid)) generated++;
            }
            foreach (var kv in FloatElipses)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillEllipse)) generated++;
            }
            foreach (var kv in Bars)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRoundedRect)) generated++;
            }
            foreach (var kv in Tabs)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRoundedRect)) generated++;
            }
            foreach (var kv in Frames)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillFrame)) generated++;
            }
            foreach (var kv in ShopMinus)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillMinus)) generated++;
            }
            foreach (var kv in ShopBuyBtn)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRoundedRect)) generated++;
            }
            foreach (var kv in ShopQtyBtn)
            {
                if (GenerateSolid(kv.Key, kv.Value.w, kv.Value.h, kv.Value.c, FillRoundedRect)) generated++;
            }

            AssetDatabase.Refresh();
            Debug.Log($"[PlaceholderArt] 已生成 {generated} 张占位图到 {PlaceholderDir}");
        }

        private static bool GenerateSolid(string name, int w, int h, Color color, System.Action<Texture2D, Color> filler, Color? bottom = null)
        {
            string path = Path.Combine(PlaceholderDir, name + ".png").Replace("\\", "/");
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            if (bottom.HasValue)
                FillGradient(tex, color, bottom.Value);
            else
                filler?.Invoke(tex, color);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(path, png);
            SetSpriteImport(path);
            return true;
        }

        /// <summary>垂直渐变填充，y 从底(0)到顶(height-1)：bottom → top。</summary>
        private static void FillGradient(Texture2D tex, Color top, Color bottom)
        {
            for (int y = 0; y < tex.height; y++)
            {
                float t = y / (float)(tex.height - 1);
                Color c = Color.Lerp(bottom, top, t);
                for (int x = 0; x < tex.width; x++)
                    tex.SetPixel(x, y, c);
            }
        }

        /// <summary>圆形填充（按钮底 / 功能图标）。</summary>
        private static void FillCircle(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float r = Mathf.Min(w, h) * 0.5f - 2f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = x - cx, dy = y - cy;
                    if (dx * dx + dy * dy <= r * r)
                        tex.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>圆角矩形填充（面板/气泡底）。</summary>
        private static void FillRoundedRect(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            float corner = Mathf.Min(w, h) * 0.18f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    // 四个圆角内侧判定
                    float dx = 0f, dy = 0f;
                    if (x < corner && y < corner) { dx = corner - x; dy = corner - y; }
                    else if (x < corner && y >= h - corner) { dx = corner - x; dy = y - (h - corner); }
                    else if (x >= w - corner && y < corner) { dx = x - (w - corner); dy = corner - y; }
                    else if (x >= w - corner && y >= h - corner) { dx = x - (w - corner); dy = y - (h - corner); }

                    if (dx * dx + dy * dy <= corner * corner)
                        tex.SetPixel(x, y, c);
                    else if (dx == 0f && dy == 0f)
                        tex.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>方框填充（中心透明，边框留边），用于物品稀有度框。</summary>
        private static void FillFrame(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            int border = Mathf.Max(3, Mathf.Min(w, h) / 10);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool onBorder = x < border || x >= w - border || y < border || y >= h - border;
                    if (onBorder) tex.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>"＋"字形填充。</summary>
        private static void FillPlus(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            int barHalf = Mathf.Max(3, w / 8);
            int centerX = w / 2, centerY = h / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inHoriz = y >= centerY - barHalf && y <= centerY + barHalf && x >= centerX - (int)(w * 0.32f) && x <= centerX + (int)(w * 0.32f);
                    bool inVert = x >= centerX - barHalf && x <= centerX + barHalf && y >= centerY - (int)(h * 0.32f) && y <= centerY + (int)(h * 0.32f);
                    if (inHoriz || inVert) tex.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>"－"字形填充。</summary>
        private static void FillMinus(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            int barHalf = Mathf.Max(3, w / 8);
            int centerX = w / 2, centerY = h / 2;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    bool inHoriz = y >= centerY - barHalf && y <= centerY + barHalf && x >= centerX - (int)(w * 0.32f) && x <= centerX + (int)(w * 0.32f);
                    if (inHoriz) tex.SetPixel(x, y, c);
                }
            }
        }

        private static bool GenerateSolid(string name, int w, int h, Color color, System.Action<Texture2D, Color> filler)
        {
            string path = Path.Combine(PlaceholderDir, name + ".png").Replace("\\", "/");
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            filler(tex, color);
            tex.Apply();
            byte[] png = tex.EncodeToPNG();
            Object.DestroyImmediate(tex);

            System.IO.File.WriteAllBytes(path, png);
            SetSpriteImport(path);
            return true;
        }

        private static void FillSolid(Texture2D tex, Color c)
        {
            for (int y = 0; y < tex.height; y++)
                for (int x = 0; x < tex.width; x++)
                    tex.SetPixel(x, y, c);
        }

        /// <summary>椭圆填充（浮漂等）。</summary>
        private static void FillEllipse(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            float cx = (w - 1) * 0.5f, cy = (h - 1) * 0.5f;
            float rx = w * 0.5f - 2f, ry = h * 0.5f - 2f;
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / rx;
                    float dy = (y - cy) / ry;
                    if (dx * dx + dy * dy <= 1f) tex.SetPixel(x, y, c);
                }
            }
        }

        /// <summary>椭圆身体 + 三角尾巴，朝右。</summary>
        private static void FillFish(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            int cx = (int)(w * 0.44f), cy = h / 2;
            int bodyRx = (int)(w * 0.32f), bodyRy = (int)(h * 0.42f);

            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float dx = (x - cx) / (float)bodyRx;
                    float dy = (y - cy) / (float)bodyRy;
                    if (dx * dx + dy * dy <= 1f)
                    {
                        tex.SetPixel(x, y, c);
                        continue;
                    }
                    // 尾巴：身体右侧的三角
                    int tailLeft = cx + bodyRx;
                    if (x >= tailLeft && x < w)
                    {
                        float t = (x - tailLeft) / (float)(w - tailLeft);
                        float half = (h * 0.22f) * t;
                        if (Mathf.Abs(y - cy) <= half) tex.SetPixel(x, y, c);
                    }
                }
            }
        }

        /// <summary>棕色长条（可视为鱼竿，左侧略粗）。</summary>
        private static void FillRod(Texture2D tex, Color c)
        {
            int w = tex.width, h = tex.height;
            int y0 = h / 2;
            int x0 = (int)(w * 0.05f);
            int x1 = (int)(w * 0.98f);
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float t = (x - x0) / (float)(x1 - x0);
                    t = Mathf.Clamp01(t);
                    float half = Mathf.Lerp(h * 0.30f, h * 0.06f, t);
                    if (x >= x0 && x <= x1 && Mathf.Abs(y - y0) <= half) tex.SetPixel(x, y, c);
                }
            }
        }

        private static void SetSpriteImport(string path)
        {
            AssetDatabase.ImportAsset(path, ImportAssetOptions.ForceUpdate);
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.mipmapEnabled = false;
            importer.SaveAndReimport();
        }
    }
}
