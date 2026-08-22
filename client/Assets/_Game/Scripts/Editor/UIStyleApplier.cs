using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using Game.Data;
using Game.UI;

namespace Game.EditorTools
{
    /// <summary>
    /// Step11：UI 尺寸/字号全局应用器（数据源 DT_UIStyle）。
    /// 按 name/父级 匹配修改预制件与场景 RectTransform.sizeDelta、字号、布局与 SafeArea；
    /// 覆盖右栏重叠根因（LayoutElement + spacing + childForceExpand）与主按钮右下偏移。
    /// </summary>
    public static class UIStyleApplier
    {
        private const string StylePath = "Assets/_Game/Data/DT_UIStyle.asset";

        [MenuItem("Tools/Step11 Apply UI Style")]
        public static void Apply()
        {
            var style = AssetDatabase.LoadAssetAtPath<UIStyleConfig>(StylePath);
            if (style == null)
            {
                Debug.LogError("[Step11] 未找到 DT_UIStyle.asset");
                return;
            }

            int fontChanged = 0, sizeChanged = 0, layoutChanged = 0;

            // ---- 遍历全部 UI 预制件 ----
            var prefabGuids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/_Game/Prefabs" });
            foreach (var guid in prefabGuids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                if (prefab == null) continue;
                ApplyToGO(prefab, style, ref fontChanged, ref sizeChanged, ref layoutChanged);
                EditorUtility.SetDirty(prefab);
            }

            // ---- 遍历场景所有 Canvas ----
            foreach (var canvas in Object.FindObjectsOfType<Canvas>(true))
            {
                ApplyToGO(canvas.gameObject, style, ref fontChanged, ref sizeChanged, ref layoutChanged);
                EditorUtility.SetDirty(canvas.gameObject);
            }

            AssetDatabase.SaveAssets();
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"[Step11] UI 样式已应用：字号 {fontChanged} 处、尺寸 {sizeChanged} 处、布局 {layoutChanged} 处");
        }

        private static void ApplyToGO(GameObject root, UIStyleConfig s, ref int fc, ref int sc, ref int lc)
        {
            bool isToast = root.name == "PF_Toast";

            // 文本
            foreach (var t in root.GetComponentsInChildren<Text>(true))
            {
                int size = TextSizeFor(t, s, isToast);
                if (size > 0 && t.fontSize != size)
                {
                    t.fontSize = size;
                    fc++;
                }
            }

            // 尺寸
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                var sz = SizeFor(rt, s);
                if (sz.HasValue && rt.sizeDelta != sz.Value)
                {
                    rt.sizeDelta = sz.Value;
                    sc++;
                }
            }

            // 右栏条目 LayoutElement（200×200，配合关闭 childForceExpand 实现整齐排列）
            foreach (var rt in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (!rt.gameObject.name.StartsWith("Item_")) continue;
                var le = rt.GetComponent<LayoutElement>();
                if (le == null) le = rt.gameObject.AddComponent<LayoutElement>();
                le.preferredWidth = s.rightItemSize.x;
                le.preferredHeight = s.rightItemSize.y;
                lc++;
            }

            // 右栏根：anchor 右中、右边距 24、spacing 24、UpperCenter、关闭 forceExpand
            if (root.name == "PF_RightBar")
            {
                var rt = root.transform as RectTransform;
                if (rt != null)
                {
                    rt.anchorMin = new Vector2(1f, 0.5f);
                    rt.anchorMax = new Vector2(1f, 0.5f);
                    rt.pivot = new Vector2(1f, 0.5f);
                    rt.anchoredPosition = new Vector2(-s.rightInset, 0f);
                    float total = 2f * s.rightItemSize.y + 1f * s.rightSpacing;
                    rt.sizeDelta = new Vector2(s.rightItemSize.x + 8f, total);
                    lc++;
                }
                var vlg = root.GetComponent<VerticalLayoutGroup>();
                if (vlg != null)
                {
                    vlg.spacing = s.rightSpacing;
                    vlg.childAlignment = TextAnchor.UpperCenter;
                    vlg.childForceExpandWidth = false;
                    vlg.childForceExpandHeight = false;
                    vlg.enabled = true;
                    lc++;
                }
                EnsureSafeArea(root, false, false, false, true);
            }

            if (root.name == "PF_TopBar")
                EnsureSafeArea(root, true, false, false, false);

            // 主按钮：右下偏移（右40、底60）
            foreach (var btn in root.GetComponentsInChildren<RectTransform>(true))
            {
                if (btn.gameObject.name == "Btn_MainAction")
                {
                    btn.anchoredPosition = s.mainButtonOffset;
                    EnsureSafeArea(btn.gameObject, false, true, false, true);
                    lc++;
                }
            }
        }

        private static Vector2? SizeFor(RectTransform rt, UIStyleConfig s)
        {
            string n = rt.gameObject.name;
            Transform p = rt.parent;
            string pn = p != null ? p.name : "";

            if (n.StartsWith("Item_")) return s.rightItemSize;
            if (n == "Img_Icon" || n == "Img_CurrencyIcon" || n == "Img_BaitIcon")
            {
                if (pn.StartsWith("Item_")) return new Vector2(s.rightIconSize, s.rightIconSize);
                if (pn == "Group_Currency" || pn == "Group_Bait") return new Vector2(s.topBarIconSize, s.topBarIconSize);
                if (pn.StartsWith("PF_InventoryItemRow")) return new Vector2(s.bagIconSize, s.bagIconSize);
                return null;
            }
            if (n == "Img_Frame" && pn.StartsWith("PF_InventoryItemRow"))
                return new Vector2(s.bagFrameSize, s.bagFrameSize);

            switch (n)
            {
                case "Btn_MainAction": return new Vector2(s.mainButtonSize, s.mainButtonSize);
                case "Btn_Reel": return new Vector2(s.reelButtonSize, s.reelButtonSize);
                case "Btn_Buy": return s.buyButtonSize;
                case "Btn_x1":
                case "Btn_x5":
                case "Btn_x10":
                case "Btn_Minus":
                case "Btn_Plus":
                case "Txt_Qty": return s.qtyButtonSize;
                case "Btn_CurrencyPlus":
                case "Btn_BaitPlus": return new Vector2(s.topBarPlusSize, s.topBarPlusSize);
                case "PF_InventoryItemRow": return new Vector2(rt.sizeDelta.x, s.bagRowHeight);
                case "PF_ShopItemRow": return new Vector2(rt.sizeDelta.x, s.shopRowHeight);
                case "Panel_Body":
                case "PF_BagPanel":
                case "PF_BaitShopPanel": return s.panelBodySize;
                case "Btn_Close": return new Vector2(s.closeButtonSize, s.closeButtonSize);
                case "Group_Currency":
                case "Group_Bait": return new Vector2(rt.sizeDelta.x, s.topBarGroupHeight);
                case "Slider_Tension": return new Vector2(rt.sizeDelta.x, s.tensionSliderHeight);
                case "Slider_CatchProgress": return new Vector2(rt.sizeDelta.x, s.catchSliderHeight);
                default: return null;
            }
        }

        private static int TextSizeFor(Text t, UIStyleConfig s, bool isToast)
        {
            if (isToast) return s.toastSize;
            switch (t.gameObject.name)
            {
                case "Txt_Title": return s.titleSize;
                case "Txt_Main": return s.mainButtonTextSize;
                case "Txt_Label": return s.rightLabelSize;
                case "Txt_Currency":
                case "Txt_Bait": return s.topCountSize;
                case "Txt_SceneName": return s.sceneNameSize;
                case "Txt_TimeOfDay": return s.timeOfDaySize;
                case "Txt_TensionState": return s.tensionStateSize;
                case "Txt_CatchProgress": return s.catchProgressSize;
                case "Txt_Bubble": return s.bubbleSize;
                case "Txt_Name":
                case "Txt_Result":
                case "Txt":
                case "Txt_": return s.bodySize;
                case "Txt_Sub":
                case "Txt_Desc":
                case "Txt_Tip":
                case "Txt_Qty":
                case "Txt_Total":
                case "Txt_UnitPrice":
                case "Txt_Sell":
                case "Txt_Primary":
                case "Txt_Empty":
                case "Txt_Count": return s.descSize;
                default: return 0;
            }
        }

        private static void EnsureSafeArea(GameObject go, bool top, bool bottom, bool left, bool right)
        {
            var c = go.GetComponent<SafeAreaController>();
            if (c == null) c = go.AddComponent<SafeAreaController>();
            c.Configure(top, bottom, left, right);
        }
    }
}
