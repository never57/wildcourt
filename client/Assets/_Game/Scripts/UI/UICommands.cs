using System;
using UnityEngine;

namespace Game.UI
{
    /// <summary>
    /// 全局 UI 命令总线：顶栏/功能栏发命令，Step 7/8 面板订阅接收。本步只发不收。
    /// </summary>
    public static class UICommands
    {
        public static event Action OnOpenBag;
        public static event Action OnOpenShop;
        public static event Action OnOpenCreel;
        public static event Action OnShopHighlight;

        public static void OpenBag()   { Debug.Log("[UI] 命令: 打开背包"); OnOpenBag?.Invoke(); }
        public static void OpenShop()  { Debug.Log("[UI] 命令: 打开商店"); OnOpenShop?.Invoke(); }
        public static void OpenCreel() { Debug.Log("[UI] 命令: 打开鱼篓"); OnOpenCreel?.Invoke(); }
        public static void HighlightShop() { Debug.Log("[UI] 命令: 高亮商店"); OnShopHighlight?.Invoke(); }
    }
}
