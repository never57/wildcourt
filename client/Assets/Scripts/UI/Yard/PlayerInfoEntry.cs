using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 庭院左上角玩家信息入口：圆形占位头像（昵称首字）+ 昵称 + ID。
    /// 常驻于 YardCanvas，不随底部面板收起隐藏；点击打开个人详情模态。
    /// </summary>
    public class PlayerInfoEntry : MonoBehaviour
    {
        private static readonly Color[] AvatarColors =
        {
            new Color(0.45f, 0.65f, 0.85f),
            new Color(0.85f, 0.60f, 0.50f),
            new Color(0.60f, 0.80f, 0.55f),
            new Color(0.80f, 0.55f, 0.75f),
            new Color(0.90f, 0.80f, 0.45f),
        };

        private UserProfileResponse _profile;
        private Image _avatarImage;
        private Text _avatarChar;
        private Text _nameText;
        private Text _idText;

        public static PlayerInfoEntry Create(Transform canvasRoot, UserProfileResponse profile)
        {
            var go = YardUiKit.CreateUiObject("PlayerInfoEntry", canvasRoot);
            var entry = go.AddComponent<PlayerInfoEntry>();
            entry.Build();
            entry.UpdateProfile(profile);
            return entry;
        }

        public void UpdateProfile(UserProfileResponse profile)
        {
            if (profile == null) return;
            _profile = profile;
            Refresh();
        }

        private void Build()
        {
            YardUiKit.SetAnchors(gameObject, new Vector2(0f, 1f), new Vector2(0f, 1f), new Vector2(12f, -60f), new Vector2(232f, -12f));

            // 半透明底 + 点击按钮
            var bg = YardUiKit.AddImage(gameObject, new Color(0.12f, 0.15f, 0.20f, 0.55f));
            var btn = gameObject.AddComponent<Button>();
            btn.targetGraphic = bg;
            btn.onClick.AddListener(() => PlayerDetailPanel.Open(transform.parent));

            // 头像（圆形占位：昵称首字 + 按 userId 选色）
            var avatarGo = YardUiKit.CreateUiObject("Avatar", transform);
            var avatarRt = avatarGo.GetComponent<RectTransform>();
            avatarRt.anchorMin = new Vector2(0f, 0.5f);
            avatarRt.anchorMax = new Vector2(0f, 0.5f);
            avatarRt.pivot = new Vector2(0f, 0.5f);
            avatarRt.anchoredPosition = new Vector2(4f, 0f);
            avatarRt.sizeDelta = new Vector2(40f, 40f);
            _avatarImage = YardUiKit.AddImage(avatarGo, Color.gray);
            _avatarImage.sprite = YardUiKit.CreateCircleSprite(64);
            _avatarChar = YardUiKit.AddText(avatarGo, "?", 18, Color.white);

            // 昵称
            var nameGo = YardUiKit.CreateUiObject("Name", transform);
            var nameRt = nameGo.GetComponent<RectTransform>();
            nameRt.anchorMin = new Vector2(0f, 0.58f);
            nameRt.anchorMax = new Vector2(1f, 1f);
            nameRt.offsetMin = new Vector2(50f, 0f);
            nameRt.offsetMax = new Vector2(-6f, 0f);
            _nameText = YardUiKit.AddText(nameGo, "", 17, Color.white);
            _nameText.alignment = TextAnchor.LowerLeft;

            // ID
            var idGo = YardUiKit.CreateUiObject("Id", transform);
            var idRt = idGo.GetComponent<RectTransform>();
            idRt.anchorMin = new Vector2(0f, 0f);
            idRt.anchorMax = new Vector2(1f, 0.52f);
            idRt.offsetMin = new Vector2(50f, 0f);
            idRt.offsetMax = new Vector2(-6f, 0f);
            _idText = YardUiKit.AddText(idGo, "", 12, new Color(0.80f, 0.85f, 0.90f));
            _idText.alignment = TextAnchor.UpperLeft;
        }

        private void Refresh()
        {
            string name = string.IsNullOrEmpty(_profile.nickname) ? "?" : _profile.nickname;
            string first = name.Substring(0, 1);

            if (_avatarImage != null)
            {
                _avatarImage.color = AvatarColor(_profile.userId);
            }
            if (_avatarChar != null) _avatarChar.text = first;
            if (_nameText != null) _nameText.text = name;
            if (_idText != null) _idText.text = "ID: " + _profile.userId;
        }

        public static Color AvatarColor(long userId)
        {
            int idx = (int)(((userId % AvatarColors.Length) + AvatarColors.Length) % AvatarColors.Length);
            return AvatarColors[idx];
        }
    }
}
