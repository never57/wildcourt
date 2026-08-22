using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Game.Env;

namespace Game.EditorTools
{
    /// <summary>
    /// 池塘背景场景构建工具：Canvas_Pond + PF_PondBackground（天空/水面/FishLayer）+
    /// PF_FishSwim 游动鱼预制件，并在 FishLayer 上挂 FishAmbientSpawner 绑定引用。
    /// </summary>
    public static class EnvSceneBuilder
    {
        private const string PrefabDir = "Assets/_Game/Prefabs/Env";
        private const string ArtDir = "Assets/_Game/Art/Placeholder";
        private const string AnimDir = "Assets/_Game/Art/Animation";

        private static readonly string[] FishSpritePaths =
        {
            ArtDir + "/PH_Fish_Crucian.png",
            ArtDir + "/PH_Fish_Carp.png",
            ArtDir + "/PH_Fish_Goldfish.png",
            ArtDir + "/PH_Fish_GrassCarp.png",
            ArtDir + "/PH_Fish_Bass.png",
        };

        [MenuItem("Tools/Build Env Scene")]
        public static void Build()
        {
            EnsureFolders();

            // ---- 清理旧物体 ----
            var old = GameObject.Find("Canvas_Pond");
            if (old != null) Object.DestroyImmediate(old);
            // 兼容：清理散落的旧 PF_PondBackground 实例
            foreach (var go in Object.FindObjectsOfType<GameObject>(true))
            {
                if (go != null && go.name == "PF_PondBackground")
                    Object.DestroyImmediate(go);
            }

            // ---- Canvas_Pond：Screen Space - Overlay，1080x1920，按高度适配 ----
            var canvasGO = new GameObject("Canvas_Pond", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = canvasGO.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = canvasGO.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080f, 1920f);
            scaler.matchWidthOrHeight = 1f;

            // ---- PF_PondBackground 层级 ----
            var bgGO = new GameObject("PF_PondBackground", typeof(RectTransform));
            bgGO.transform.SetParent(canvasGO.transform, false);
            Stretch(bgGO.GetComponent<RectTransform>());

            // Img_Sky：上半屏
            var skyRT = CreateImage(bgGO.transform, "Img_Sky",
                new Vector2(0f, 0.5f), new Vector2(1f, 1f), ArtDir + "/PH_Sky_Sunset.png");
            // Img_Water：下半屏
            var waterRT = CreateImage(bgGO.transform, "Img_Water",
                new Vector2(0f, 0f), new Vector2(1f, 0.5f), ArtDir + "/PH_Water.png");

            // FishLayer：铺满下半屏
            var layerGO = new GameObject("FishLayer", typeof(RectTransform));
            layerGO.transform.SetParent(bgGO.transform, false);
            var lrt = layerGO.GetComponent<RectTransform>();
            lrt.anchorMin = new Vector2(0f, 0f);
            lrt.anchorMax = new Vector2(1f, 0.5f);
            lrt.offsetMin = Vector2.zero;
            lrt.offsetMax = Vector2.zero;

            // ---- 保存 PF_PondBackground ----
            PrefabUtility.SaveAsPrefabAsset(bgGO, PrefabDir + "/PF_PondBackground.prefab");

            // ---- PF_FishSwim 预制件 ----
            var fishGO = new GameObject("PF_FishSwim", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Animator));
            var fimg = fishGO.GetComponent<Image>();
            fimg.sprite = LoadSprite(ArtDir + "/PH_Fish_Crucian.png");
            fimg.raycastTarget = false;
            var anim = fishGO.GetComponent<Animator>();
            anim.runtimeAnimatorController =
                AssetDatabase.LoadAssetAtPath<RuntimeAnimatorController>(AnimDir + "/AC_Fish.controller");
            fishGO.AddComponent<FishSwimController>();
            var frt = fishGO.GetComponent<RectTransform>();
            frt.sizeDelta = new Vector2(256f, 128f);
            frt.anchorMin = frt.anchorMax = new Vector2(0.5f, 0.5f);
            PrefabUtility.SaveAsPrefabAsset(fishGO, PrefabDir + "/PF_FishSwim.prefab");
            Object.DestroyImmediate(fishGO);

            // ---- 场景实例化 PF_PondBackground ----
            var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/PF_PondBackground.prefab");
            var instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.transform.SetParent(canvasGO.transform, false);
            instance.name = "PF_PondBackground";

            // ---- FishLayer 挂 FishAmbientSpawner 并绑定引用 ----
            var layerTrans = instance.transform.Find("FishLayer");
            var spawner = layerTrans.gameObject.GetComponent<FishAmbientSpawner>();
            if (spawner == null) spawner = layerTrans.gameObject.AddComponent<FishAmbientSpawner>();

            var so = new SerializedObject(spawner);
            so.FindProperty("fishPrefab").objectReferenceValue =
                AssetDatabase.LoadAssetAtPath<GameObject>(PrefabDir + "/PF_FishSwim.prefab");
            so.FindProperty("fishCount").intValue = 8;
            var spritesProp = so.FindProperty("fishSprites");
            spritesProp.arraySize = FishSpritePaths.Length;
            for (int i = 0; i < FishSpritePaths.Length; i++)
                spritesProp.GetArrayElementAtIndex(i).objectReferenceValue = LoadSprite(FishSpritePaths[i]);
            so.ApplyModifiedProperties();

            // ---- Apply 回预制件并保存场景 ----
            PrefabUtility.ApplyPrefabInstance(instance, InteractionMode.AutomatedAction);
            EditorSceneManager.MarkSceneDirty(instance.scene);
            EditorSceneManager.SaveOpenScenes();

            AssetDatabase.SaveAssets();
            Debug.Log("[Env] 场景构建完成：Canvas_Pond / PF_PondBackground / PF_FishSwim 就绪");
        }

        private static void EnsureFolders()
        {
            if (!AssetDatabase.IsValidFolder("Assets/_Game/Prefabs"))
                AssetDatabase.CreateFolder("Assets/_Game", "Prefabs");
            if (!AssetDatabase.IsValidFolder(PrefabDir))
                AssetDatabase.CreateFolder("Assets/_Game/Prefabs", "Env");
        }

        private static void Stretch(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static RectTransform CreateImage(Transform parent, string name, Vector2 anchorMin, Vector2 anchorMax, string spritePath)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.transform.SetParent(parent, false);
            var rt = go.GetComponent<RectTransform>();
            rt.anchorMin = anchorMin;
            rt.anchorMax = anchorMax;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
            var img = go.GetComponent<Image>();
            img.sprite = LoadSprite(spritePath);
            img.type = Image.Type.Simple;
            img.raycastTarget = false;
            return rt;
        }

        private static Sprite LoadSprite(string path)
        {
            return AssetDatabase.LoadAssetAtPath<Sprite>(path);
        }
    }
}
