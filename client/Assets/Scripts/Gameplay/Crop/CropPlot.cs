using System;
using UnityEngine;

/// <summary>
/// 单块农田状态机。
/// 阶段：空地 -> 播种 -> 萌芽(25%) -> 生长(50%) -> 成熟(100%)。
/// 对应文档四 4.3 种植规则、4.4 生长阶段。
/// </summary>
public class CropPlot
{
    public enum PlotState
    {
        Empty,    // 空地
        Growing,  // 生长中（含播种/萌芽/生长）
        Mature    // 成熟可收获
    }

    public int Index { get; }
    public PlotState State { get; private set; } = PlotState.Empty;
    public string CropId { get; private set; }
    public float PlantTime { get; private set; }  // 播种时刻（秒，Time.time 基准）
    public int GrowDurationSec { get; private set; }

    public event Action<CropPlot> OnStateChanged;

    public CropPlot(int index) => Index = index;

    /// <summary>播种：开始一段 growTime 分钟的生长。</summary>
    public void Plant(string cropId, int growTimeMin)
    {
        CropId = cropId;
        GrowDurationSec = growTimeMin * 60;
        PlantTime = Time.time;
        State = PlotState.Growing;
        OnStateChanged?.Invoke(this);
    }

    /// <summary>播种：直接指定生长秒数（本地调试/加速用，分钟版见 <see cref="Plant"/>）。</summary>
    public void PlantSeconds(string cropId, float growSeconds)
    {
        CropId = cropId;
        GrowDurationSec = Mathf.Max(1, Mathf.RoundToInt(growSeconds));
        PlantTime = Time.time;
        State = PlotState.Growing;
        OnStateChanged?.Invoke(this);
    }

    /// <summary>恢复播种：按完整生长时长 + 已生长秒数重建（进度从 elapsed 处继续显示，
    /// 进度条公式 = 已生长/总时长 = 1 - 剩余/总时长；成熟时刻 = now + (totalSec - elapsedSec)）。</summary>
    public void PlantWithElapsed(string cropId, float totalSec, float elapsedSec)
    {
        PlantSeconds(cropId, totalSec);
        PlantTime = Time.time - Mathf.Clamp(elapsedSec, 0f, GrowDurationSec - 1f);
    }

    /// <summary>每帧/定时推进，判断是否成熟。</summary>
    public void Tick()
    {
        if (State != PlotState.Growing) return;
        if (Time.time - PlantTime >= GrowDurationSec)
        {
            State = PlotState.Mature;
            OnStateChanged?.Invoke(this);
        }
    }

    /// <summary>生长进度 0~1（用于切换 4 阶段 Sprite，文档四 4.4）。</summary>
    public float Progress => GrowDurationSec <= 0 ? 1f : Mathf.Clamp01((Time.time - PlantTime) / GrowDurationSec);

    public int GrowthStage()
    {
        var p = Progress;
        if (State == PlotState.Mature || p >= 1f) return 4; // 成熟
        if (p >= 0.5f) return 3; // 生长
        if (p >= 0.25f) return 2; // 萌芽
        return 1; // 播种
    }

    public void Harvest()
    {
        State = PlotState.Empty;
        CropId = null;
        OnStateChanged?.Invoke(this);
    }

    /// <summary>施加化肥立即完成生长（文档四 4.3，化肥为邮券道具）。</summary>
    public void ForceMature()
    {
        if (State == PlotState.Growing)
        {
            State = PlotState.Mature;
            OnStateChanged?.Invoke(this);
        }
    }
}
