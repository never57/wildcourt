using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using Unity.VisualScripting;
using System;

public enum MessageSender
{
    Me,
    Friend
}

public class ChatManner : MonoBehaviour
{
    [Header("窗口控制")]
    public GameObject chatWindow;  //聊天窗口
    public Button chatButton;  //打开
    public Button closeButton;  //关闭

    [Header("好友列表")]
    public Transform friendListContent;  //好友按钮
    public GameObject friendButtonPrefab;  //好友按钮预制体

    [Header("消息列表")]
    public Transform messContent; // 存放消息
    public GameObject myMessagePrefab;  // 本人消息预制体
    public GameObject friendMessagePrefab;  // 好友消息预制体

    [Header("输入框")]
    public TMP_InputField inputField;  // 输入框
    public Button sendButton;  // 发送按钮  

    [Header("时间标签")]
    public GameObject timeLabelPrefab;  //时间标签预制体喵
    private string lastMessageTime = "";  //上条消息时间喵

    [Header("头像")]
    public Sprite myAvatar;  // 本人头像喵
    public Sprite friendAvatar;  // 好友头像喵

    void Start()
    {
        inputField.text = "";
        //chatWindow.SetActive(false);
        //将窗口组件全部禁止喵
        chatWindow.GetComponent<CanvasGroup>().blocksRaycasts = false;
        chatWindow.GetComponent<CanvasGroup>().alpha = 0;
        chatWindow.GetComponent<CanvasGroup>().interactable = false;

        //绑定按钮事件喵
        chatButton.onClick.AddListener(OpenChat);
        closeButton.onClick.AddListener(ChatClose);
        sendButton.onClick.AddListener(SendMessage);

        //测试
        AddFriend("Alice");
        AddFriend("Lian");
        AddFriend("Kisaki");

        AddMessage("Kisaki:sukidesu", MessageSender.Friend);
        AddMessage("Lian:watashi mo suki desu", MessageSender.Me);

    }

    public void OpenChat()
    {
        //启用窗口组件喵
        //chatWindow.SetActive(true);
        chatWindow.GetComponent<CanvasGroup>().blocksRaycasts = true;
        chatWindow.GetComponent<CanvasGroup>().alpha = 1;
        chatWindow.GetComponent<CanvasGroup>().interactable = true;
    }

    public void ChatClose()
    {
        //禁用窗口组件喵
        //chatWindow.SetActive(false);
        chatWindow.GetComponent<CanvasGroup>().blocksRaycasts = false;
        chatWindow.GetComponent<CanvasGroup>().alpha = 0;
        chatWindow.GetComponent<CanvasGroup>().interactable = false;
    }

    void AddFriend(string name)
    {
        GameObject newFirend = Instantiate(friendButtonPrefab, friendListContent);

        newFirend.GetComponentInChildren<TMP_Text>().text = name;
        Debug.LogWarning("添加正常" + name);
    }

/*    void AddMessage(string text)
    {
        GameObject newMsg = Instantiate(messagePrefab, messContent);

        newMsg.GetComponentInChildren<TMP_Text>().text = text;
    }*/

    void SendMessage()
    {
        string msg = inputField.text;
        if(!string.IsNullOrEmpty(msg))
        {
            AddMessage(msg);

            inputField.text = "";
        }
    }

    void AddMessage(string text,MessageSender sender = MessageSender.Me)
    {
        

        //检查是否需要显示时间喵
        string currentTime = System.DateTime.Now.ToString("HH:mm");
        if (ShouldShowTime())
        {
            //创建时间预制体
            GameObject timeObj = Instantiate(timeLabelPrefab, messContent);
            TMP_Text timeText = timeObj.GetComponentInChildren<TMP_Text>();
            if (timeText != null) timeText.text = currentTime;
            lastMessageTime = currentTime;
        }

        GameObject PrefabToUse = (sender == MessageSender.Me) ? myMessagePrefab : friendMessagePrefab;

        GameObject msgObj = Instantiate(PrefabToUse, messContent);
        //获取文字组件喵
        TMP_Text msgText = msgObj.GetComponentInChildren<TMP_Text>();
        if (msgText != null) msgText.text = text;

        Canvas.ForceUpdateCanvases(); // 强制更新布局喵
        float textWidth = msgText.preferredWidth; // 获取文本的宽度喵
        float textHeight = msgText.preferredHeight; // 获取文本的高度喵
        RectTransform bgRect = msgObj.transform.Find("Background").GetComponent<RectTransform>(); // 获取背景的 RectTransform 喵"
        float padding = 10f; // 设置背景的内边距喵
        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth + padding); // 设置背景的宽度喵
        bgRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, textHeight + padding); // 设置背景的高度喵

        //设置头像喵
        Image avatar  = msgObj.transform.Find("Avatar").GetComponent<Image>();
        avatar.sprite = (sender == MessageSender.Me) ? myAvatar : friendAvatar;

        //获取背景组件喵
        /*Image background = msgObj.GetComponentInChildren<Image>();

        if(background != null)
        {
            if(sender == MessageSender.Me)
            {
                background.color = new Color(0.2f, 0.6f, 1f); // 设置蓝色背景喵
                msgText.color = Color.white;  //设施白色字体喵
                //设置右对齐喵
                msgObj.GetComponent<RectTransform>().anchorMin = new Vector2(1, 0);
                msgObj.GetComponent<RectTransform>().anchorMax = new Vector2(1, 0);
                msgObj.GetComponent<RectTransform>().pivot = new Vector2(0, 0.5f);
                msgObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(-20, 0);
            }
            else
            {
                background.color = new Color(0.8f, 0.8f, 0.8f); // 设置灰色背景喵
                msgText.color = Color.black;  //设施黑色字体喵
                //设置左对齐喵
                msgObj.GetComponent<RectTransform>().anchorMin = new Vector2(0, 0.5f);
                msgObj.GetComponent<RectTransform>().anchorMax = new Vector2(0, 0.5f);
                msgObj.GetComponent<RectTransform>().pivot = new Vector2(0.5f, 0.5f);
                msgObj.GetComponent<RectTransform>().anchoredPosition = new Vector2(20, 0);
            }
        }*/
    }

    bool ShouldShowTime()
    {
        string currentTime = System.DateTime.Now.ToString("HH:mm");
        if (string.IsNullOrEmpty(lastMessageTime)) return true;

        DateTime last = System.DateTime.ParseExact(lastMessageTime, "HH:mm", null);
        DateTime now = System.DateTime.Now;
        TimeSpan diff = now - last;

        return diff.TotalMinutes >= 3;
    }

}
