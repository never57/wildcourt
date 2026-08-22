using System;
using UnityEditor;
using UnityEngine;
using Game.Data;
using Game.Services;

namespace Game.EditorTools
{
    /// <summary>
    /// InventoryService 编辑器验证工具：逐条执行用例并打印 PASS/FAIL。
    /// </summary>
    public static class InventoryServiceValidator
    {
        private const string DataTablePath = "Assets/_Game/Data/Fishing/DT_Fishing.asset";

        [MenuItem("Tools/Validate Inventory Service")]
        public static void Validate()
        {
            var table = AssetDatabase.LoadAssetAtPath<FishingDataTable>(DataTablePath);
            if (table == null)
            {
                Debug.LogError($"[Inventory] 校验失败：未找到数据表 {DataTablePath}");
                return;
            }

            var svc = InventoryService.Instance;
            svc.Init(table);

            int passCount = 0;

            // 用例1：Init 初始值
            bool c1 = svc.Currency == 478 && svc.GetCount("B01") == 6
                      && svc.SelectedBait != null && svc.SelectedBait.id == "B01";
            LogResult(1, c1);
            if (c1) passCount++;

            // 用例2：消费货币
            bool spent = svc.TrySpendCurrency(478);
            bool c2 = spent && svc.Currency == 0 && !svc.TrySpendCurrency(1);
            LogResult(2, c2);
            if (c2) passCount++;

            // 用例3：加物品并订阅事件计数
            int evtCount = 0;
            Action<string, int> listener = (id, c) => { if (id == "F02") evtCount++; };
            svc.OnItemCountChanged += listener;
            svc.AddItem("F02", 3);
            svc.OnItemCountChanged -= listener;
            bool c3 = svc.GetCount("F02") == 3 && evtCount == 1;
            LogResult(3, c3);
            if (c3) passCount++;

            // 用例4：移除物品
            int before = svc.GetCount("F02");
            bool failRemove = svc.TryRemoveItem("F02", 4);
            int afterFail = svc.GetCount("F02");
            bool okRemove = svc.TryRemoveItem("F02", 3);
            int afterOk = svc.GetCount("F02");
            bool c4 = !failRemove && before == afterFail && okRemove && afterOk == 0;
            LogResult(4, c4);
            if (c4) passCount++;

            // 用例5：切换鱼饵
            int baitEvt = 0;
            Action<BaitConfig> baitListener = b => baitEvt++;
            svc.OnSelectedBaitChanged += baitListener;
            bool okSelect = svc.TrySelectBait("B03");
            svc.OnSelectedBaitChanged -= baitListener;
            bool badSelect = svc.TrySelectBait("XXX");
            bool c5 = okSelect && baitEvt == 1 && !badSelect;
            LogResult(5, c5);
            if (c5) passCount++;

            // 用例6：恢复初始值
            svc.Init(table);
            bool c6 = svc.Currency == 478 && svc.GetCount("B01") == 6
                      && svc.SelectedBait != null && svc.SelectedBait.id == "B01";
            LogResult(6, c6);
            if (c6) passCount++;
            svc.LogState();
            Debug.Log($"[Inventory] 服务层校验完成：{passCount}/6 PASS");
        }

        private static void LogResult(int index, bool ok)
        {
            Debug.Log($"[Inventory] 用例{index}：{(ok ? "PASS" : "FAIL")}");
        }
    }
}
