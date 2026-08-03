using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 农田管理：持有所有 CropPlot。
/// 初始 6 块、最大 12 块（文档四 4.3；最大数由庭院解锁进度 4 级决定，见 Sheet16）。
/// </summary>
public class FarmManager
{
    private readonly List<CropPlot> _plots = new List<CropPlot>();
    public int MaxPlots { get; private set; }
    public IReadOnlyList<CropPlot> Plots => _plots;

    public void Init(int initial, int max)
    {
        MaxPlots = max;
        for (int i = 0; i < initial; i++)
            _plots.Add(new CropPlot(i));
    }

    /// <summary>用土地卡扩充农田（文档九 9.7，最多到 12 块）。</summary>
    public bool Expand()
    {
        if (_plots.Count >= MaxPlots) return false;
        _plots.Add(new CropPlot(_plots.Count));
        return true;
    }

    public CropPlot GetPlot(int index) => index >= 0 && index < _plots.Count ? _plots[index] : null;

    /// <summary>找一块空地播种；无空地返回 null。</summary>
    public CropPlot GetEmptyPlot()
    {
        foreach (var p in _plots)
            if (p.State == CropPlot.PlotState.Empty) return p;
        return null;
    }

    public void TickAll()
    {
        foreach (var p in _plots) p.Tick();
    }
}
