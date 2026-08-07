using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.Yard
{
    /// <summary>
    /// 个人详情模态面板：全屏半透明遮罩（点击关闭）+ 居中详情卡。
    /// 打开时重新拉取 GET /user/profile 保证最新。
    /// </summary>
    public class PlayerDetailPanel : MonoBehaviour
    {
        private static PlayerDetailPanel _instance;

        private Text _nameText;
        private Text _idText;
        private Text _levelText;
        private Text _shellText;
        private Text _couponText;
        private Text _provinceText;
        private Text _cityText;
        private Text _avatarChar;
        private Image _avatarImage;

        public static void Open(Transform canvasRoot)
        {
            if (_instance != null) return; // 已打开
            var go = YardUiKit.CreateUiObject("PlayerDetailPanel", canvasRoot);
            var panel = go.AddComponent<PlayerDetailPanel>();
            _instance = panel;
            panel.Build();
            panel.RefreshFromServer();
        }

        public static void Close()
        {
            if (_instance != null)
            {
                Destroy(_instance.gameObject);
                _instance = null;
            }
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
        }

        private void Build()
        {
            // 全屏遮罩（点击关闭）
            YardUiKit.SetAnchors(gameObject, Vector2.zero, Vector2.one, Vector2.zero, Vector2.zero);
            var mask = YardUiKit.AddImage(gameObject, new Color(0f, 0f, 0f, 0.55f));
            var maskBtn = gameObject.AddComponent<Button>();
            maskBtn.targetGraphic = mask;
            maskBtn.onClick.AddListener(Close);

            // 详情卡
            var card = YardUiKit.CreateUiObject("Card", transform);
            YardUiKit.SetAnchors(card, new Vector2(0.5f, 0.5f), new Vector2(0.5f, 0.5f), new Vector2(-220f, -280f), new Vector2(220f, 180f));
            YardUiKit.AddImage(card, new Color(0.20f, 0.24f, 0.30f, 0.98f));

            // 关闭按钮（×）
            var closeGo = YardUiKit.CreateUiObject("Close", card.transform);
            var closeRt = closeGo.GetComponent<RectTransform>();
            closeRt.anchorMin = new Vector2(1f, 1f);
            closeRt.anchorMax = new Vector2(1f, 1f);
            closeRt.pivot = new Vector2(1f, 1f);
            closeRt.anchoredPosition = new Vector2(-6f, -6f);
            closeRt.sizeDelta = new Vector2(34f, 34f);
            var closeBtn = YardUiKit.AddButton(closeGo, "×", Close);
            closeBtn.targetGraphic = closeGo.GetComponent<Image>();

            // 头像
            var avatarGo = YardUiKit.CreateUiObject("Avatar", card.transform);
            var avatarRt = avatarGo.GetComponent<RectTransform>();
            avatarRt.anchorMin = new Vector2(0.5f, 1f);
            avatarRt.anchorMax = new Vector2(0.5f, 1f);
            avatarRt.pivot = new Vector2(0.5f, 1f);
            avatarRt.anchoredPosition = new Vector2(0f, -24f);
            avatarRt.sizeDelta = new Vector2(64f, 64f);
            _avatarImage = YardUiKit.AddImage(avatarGo, Color.gray);
            _avatarImage.sprite = YardUiKit.CreateCircleSprite(64);
            _avatarChar = YardUiKit.AddText(avatarGo, "?", 26, Color.white);

            // 昵称
            var nameGo = YardUiKit.CreateUiObject("Name", card.transform);
            YardUiKit.SetAnchors(nameGo, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-160f, -104f), new Vector2(160f, -80f));
            _nameText = YardUiKit.AddText(nameGo, "", 20, Color.white);

            // 信息行
            _idText = AddRow(card, 0, "ID");
            _levelText = AddRow(card, 1, "等级/经验");
            _shellText = AddRow(card, 2, "庭贝");
            _couponText = AddRow(card, 3, "邮券");
            _provinceText = AddRow(card, 4, "省份");
            _cityText = AddRow(card, 5, "城市");
        }

        private Text AddRow(GameObject card, int index, string label)
        {
            var row = YardUiKit.CreateUiObject("Row" + index, card.transform);
            float y = -124f - index * 25f;
            YardUiKit.SetAnchors(row, new Vector2(0.5f, 1f), new Vector2(0.5f, 1f), new Vector2(-200f, y - 22f), new Vector2(200f, y));

            var labelGo = YardUiKit.CreateUiObject("Label", row.transform);
            YardUiKit.SetStretch(labelGo);
            var labelText = YardUiKit.AddText(labelGo, label, 13, new Color(0.70f, 0.75f, 0.82f));
            labelText.alignment = TextAnchor.MiddleLeft;

            var valueGo = YardUiKit.CreateUiObject("Value", row.transform);
            YardUiKit.SetStretch(valueGo);
            var valueText = YardUiKit.AddText(valueGo, "-", 13, Color.white);
            valueText.alignment = TextAnchor.MiddleRight;
            return valueText;
        }

        private void RefreshFromServer()
        {
            if (NetManager.Instance == null) return;
            NetManager.Instance.Get<UserProfileResponse>(ApiPaths.UserProfile,
                resp => Fill(resp.data),
                (code, msg) => Debug.LogWarning($"[PlayerDetail] profile 加载失败: {msg}"));
        }

        private void Fill(UserProfileResponse profile)
        {
            if (profile == null) return;
            string name = string.IsNullOrEmpty(profile.nickname) ? "?" : profile.nickname;
            string first = name.Substring(0, 1);
            if (_avatarImage != null) _avatarImage.color = PlayerInfoEntry.AvatarColor(profile.userId);
            if (_avatarChar != null) _avatarChar.text = first;
            if (_nameText != null) _nameText.text = name;
            if (_idText != null) _idText.text = profile.userId.ToString();
            if (_levelText != null) _levelText.text = $"Lv {profile.level} / 经验 {profile.exp}";
            if (_shellText != null) _shellText.text = profile.shell.ToString();
            if (_couponText != null) _couponText.text = profile.coupon.ToString();
            if (_provinceText != null) _provinceText.text = profile.provinceId.ToString();
            if (_cityText != null) _cityText.text = profile.cityId.ToString();
        }
    }
}
