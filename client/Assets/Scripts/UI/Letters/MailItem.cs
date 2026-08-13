using UnityEngine;
using UnityEngine.UI;
using WildCourt.Net.Protocol;

namespace WildCourt.UI.Letters
{
    /// <summary>信箱列表项（mailItemPrefab 组件，字段在预制体中绑定）。</summary>
    public class MailItem : MonoBehaviour
    {
        [SerializeField] private Text fromText;
        [SerializeField] private Text contentText;
        [SerializeField] private Text statusText;
        [SerializeField] private Text timeText;
        [SerializeField] private Button button;

        public LetterItem Letter { get; private set; }

        public void Bind(LetterItem letter, System.Action<LetterItem> onClick)
        {
            Letter = letter;
            if (fromText != null) fromText.text = letter.fromUserId == 0 ? "官方" : letter.fromNickname;
            if (contentText != null) contentText.text = letter.content;
            if (statusText != null) statusText.text = letter.status;
            if (timeText != null) timeText.text = FormatTime(letter.createdAt);
            if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick(letter));
            }
        }

        private static string FormatTime(long unixMs)
        {
            if (unixMs <= 0) return string.Empty;
            var t = System.DateTimeOffset.FromUnixTimeMilliseconds(unixMs).ToLocalTime();
            return t.ToString("MM-dd HH:mm");
        }
    }
}
