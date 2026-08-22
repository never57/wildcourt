using System;
using UnityEngine;
using WildCourt.Net;
using WildCourt.Net.Protocol;

namespace WildCourt.Gameplay.Fishing
{
    /// <summary>
    /// 钓鱼事件上报器：复用项目现有 <see cref="NetManager"/> 的 REST POST 能力，
    /// 不引入任何新的网络库。钓鱼成功/失败结算时各触发一次上报。
    /// <para>上报路径：<c>/api/v1/fishing/event</c>（后端管理端接收）。</para>
    /// <para>场景中无 NetManager（未登录/独立测试场景）时仅打印告警，不阻断玩法。</para>
    /// </summary>
    public static class FishingReporter
    {
        /// <summary>后端钓鱼事件接收接口路径（与 ApiPaths.Base = /api/v1 拼接）。</summary>
        public const string ReportPath = "/api/v1/fishing/event";

        /// <summary>
        /// 上报一次钓鱼结算事件。
        /// </summary>
        /// <param name="result">结算结果（成功/失败、鱼种、原因）。</param>
        /// <param name="baitId">本次消耗的鱼饵ID。</param>
        public static void Report(FishingResult result, string baitId)
        {
            // 复用现有网络管理器；若场景中不存在（独立测试/未登录）则跳过，保证玩法不被阻断
            if (NetManager.Instance == null)
            {
                Debug.LogWarning("[FishingReporter] 场景无 NetManager，跳过事件上报");
                return;
            }

            var dto = new FishingEventDto
            {
                eventType = result.Success ? "success" : "fail",
                baitId = baitId ?? string.Empty,
                fishId = result.Fish != null ? result.Fish.id : null,
                fishName = result.Fish != null ? result.Fish.name : null,
                reason = result.Reason ?? string.Empty,
                timestampMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            };

            // 复用 NetManager.Post：内部经 JsonUtil 序列化为 JSON、注入 token、带幂等键
            NetManager.Instance.Post<object>(ReportPath, dto,
                ok => Debug.Log($"[FishingReporter] 上报成功 eventType={dto.eventType}"),
                (code, msg) => Debug.LogWarning($"[FishingReporter] 上报失败 code={code} msg={msg}"));
        }
    }

    /// <summary>
    /// 钓鱼事件上报 DTO（POST /api/v1/fishing/event 请求体）。
    /// 字段命名与后端约定一致，JsonUtil 默认按 PascalCase 序列化，后端按需映射。
    /// </summary>
    [Serializable]
    public class FishingEventDto
    {
        public string eventType;     // "success" | "fail"
        public string baitId;       // 本次消耗的鱼饵ID
        public string fishId;       // 成功时为鱼种ID，失败为 null
        public string fishName;     // 成功时为鱼名，失败为 null
        public string reason;      // 失败原因；成功为钓获说明
        public long timestampMs;    // 事件发生的 Unix 毫秒时间戳
    }
}
