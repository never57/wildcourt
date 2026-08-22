using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using Game.Data;

namespace Game.EditorTools
{
    /// <summary>
    /// 钓鱼数据自检工具：校验 DT_Fishing 中鱼/饵数据的完整性、唯一性、权重与随机分布。
    /// </summary>
    public static class FishingDataValidator
    {
        private const string DataTablePath = "Assets/_Game/Data/Fishing/DT_Fishing.asset";

        [MenuItem("Tools/Validate Fishing Data")]
        public static void Validate()
        {
            bool ok = true;
            var errors = new List<string>();

            var table = AssetDatabase.LoadAssetAtPath<FishingDataTable>(DataTablePath);
            if (table == null)
            {
                errors.Add($"未找到数据表: {DataTablePath}");
                ok = false;
            }

            if (table != null)
            {
                // --- 鱼：唯一性与 icon ---
                var fishIds = new Dictionary<string, string>();
                if (table.fishes == null || table.fishes.Length != 5)
                {
                    errors.Add($"鱼数量错误，期望 5，实际 {(table.fishes == null ? 0 : table.fishes.Length)}");
                    ok = false;
                }
                else
                {
                    foreach (var f in table.fishes)
                    {
                        if (f == null) { errors.Add("存在空鱼配置"); ok = false; continue; }
                        if (string.IsNullOrEmpty(f.id)) { errors.Add($"鱼 {f.name} 的 id 为空"); ok = false; }
                        else if (fishIds.ContainsKey(f.id)) { errors.Add($"鱼 id 重复: {f.id}"); ok = false; }
                        else fishIds[f.id] = f.id;
                        if (f.icon == null) { errors.Add($"鱼 {f.id} 缺少 icon"); ok = false; }
                    }
                }

                // --- 饵：唯一性、权重和==100、icon ---
                var baitIds = new Dictionary<string, string>();
                if (table.baits == null || table.baits.Length != 3)
                {
                    errors.Add($"饵数量错误，期望 3，实际 {(table.baits == null ? 0 : table.baits.Length)}");
                    ok = false;
                }
                else
                {
                    foreach (var b in table.baits)
                    {
                        if (b == null) { errors.Add("存在空饵配置"); ok = false; continue; }
                        if (string.IsNullOrEmpty(b.id)) { errors.Add($"饵 {b.name} 的 id 为空"); ok = false; }
                        else if (baitIds.ContainsKey(b.id)) { errors.Add($"饵 id 重复: {b.id}"); ok = false; }
                        else baitIds[b.id] = b.id;
                        if (b.icon == null) { errors.Add($"饵 {b.id} 缺少 icon"); ok = false; }
                        if (b.catchWeights == null || b.catchWeights.Length != 5)
                        {
                            errors.Add($"饵 {b.id} 的 catchWeights 长度应为 5，实际 {(b.catchWeights == null ? 0 : b.catchWeights.Length)}");
                            ok = false;
                        }
                        else
                        {
                            int sum = 0;
                            foreach (var w in b.catchWeights) sum += w;
                            if (sum != 100)
                            {
                                errors.Add($"饵 {b.id} 的 catchWeights 之和为 {sum}，应为 100");
                                ok = false;
                            }
                        }
                    }
                }

                // --- 分布校验：固定 seed 连跑 1000 次，与权重偏差 <5% ---
                if (table.baits != null)
                {
                    foreach (var b in table.baits)
                    {
                        if (b == null || b.catchWeights == null || b.catchWeights.Length != 5) continue;
                        var rng = new System.Random(20260821);
                        var counts = new int[5];
                        const int trials = 1000;
                        for (int i = 0; i < trials; i++) counts[(int)b.RollFish(rng)]++;
                        for (int s = 0; s < 5; s++)
                        {
                            float expect = b.catchWeights[s] / 100f;
                            float actual = counts[s] / (float)trials;
                            float dev = Mathf.Abs(actual - expect);
                            if (dev > 0.05f)
                            {
                                errors.Add($"饵 {b.id} 鱼种 {s} 分布偏差 {dev:P1}，期望 {expect:P1}，实际 {actual:P1}");
                                ok = false;
                            }
                        }
                    }
                }
            }

            if (ok)
            {
                Debug.Log("[Fishing] 数据校验通过：鱼5 饵3");
            }
            else
            {
                foreach (var e in errors) Debug.LogError("[Fishing] " + e);
                Debug.LogError("[Fishing] 数据校验失败");
            }
        }
    }
}
