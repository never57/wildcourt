using System.Collections;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace WildCourt.Gameplay.Yard
{
    /// <summary>
    /// 场景预置地面（提前摆放）：异步加载 StreamingAssets/ui/Yard/ground.png（可热更），
    /// 无图/失败时用运行时菱形占位贴图兜底；尺寸与 sortingOrder 由场景里的 Transform 设置。
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class YardGround : MonoBehaviour
    {
        private SpriteRenderer _sr;

        private void Awake()
        {
            _sr = GetComponent<SpriteRenderer>();
            if (_sr == null) return;
            ApplyPlaceholder();
            StartCoroutine(LoadArt());
        }

        private void ApplyPlaceholder()
        {
            _sr.sprite = CreateDiamondPlaceholder();
            _sr.color = new Color(0.72f, 0.88f, 0.62f);
        }

        private IEnumerator LoadArt()
        {
            string path = Path.Combine(Application.streamingAssetsPath, "ui", "Yard", "ground.png");
            string url = Application.platform == RuntimePlatform.Android ? path : "file://" + path.Replace('\\', '/');
            using (var request = UnityWebRequestTexture.GetTexture(url))
            {
                yield return request.SendWebRequest();
                if (request.result == UnityWebRequest.Result.Success)
                {
                    var tex = DownloadHandlerTexture.GetContent(request);
                    if (tex != null && _sr != null)
                    {
                        _sr.sprite = Sprite.Create(tex, new Rect(0, 0, tex.width, tex.height), new Vector2(0.5f, 0.5f), 100f);
                        _sr.color = Color.white;
                    }
                }
            }
        }

        private static Sprite CreateDiamondPlaceholder()
        {
            const int size = 32;
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
                    pixels[y * size + x] = dx + dy <= radius ? Color.white : Color.clear;
                }
            }
            tex.SetPixels(pixels);
            tex.Apply();
            return Sprite.Create(tex, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        }
    }
}