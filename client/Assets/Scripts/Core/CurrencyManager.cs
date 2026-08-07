using System;
using UnityEngine;

/// <summary>
/// 货币管理：庭贝（日常软货币）+ 邮券（活动限定货币）。
/// 对应文档九 9.1。邮券不可购买、不可兑换庭贝、活动结束不清零。
/// </summary>
public class CurrencyManager
{
    public int Shell { get; private set; }   // 庭贝（文档九 9.1）
    public int Stamp { get; private set; }   // 邮券（文档九 9.1.1）

    public event Action<int, int> OnCurrencyChanged;

    /// <summary>服务端快照同步（阶段 C：登录/购买后对齐权威数值）。</summary>
    public void Set(int shell, int coupon)
    {
        Shell = shell;
        Stamp = coupon;
        OnCurrencyChanged?.Invoke(Shell, Stamp);
    }

    public void AddShell(int amount)
    {
        if (amount < 0) return;
        Shell += amount;
        OnCurrencyChanged?.Invoke(Shell, Stamp);
    }

    public bool SpendShell(int amount)
    {
        if (amount < 0 || Shell < amount) return false;
        Shell -= amount;
        OnCurrencyChanged?.Invoke(Shell, Stamp);
        return true;
    }

    public void AddStamp(int amount)
    {
        if (amount < 0) return;
        Stamp += amount;
        OnCurrencyChanged?.Invoke(Shell, Stamp);
    }

    /// <summary>
    /// 邮券不可逆向兑换庭贝（文档九 9.1.1 规则）。
    /// </summary>
    public bool SpendStamp(int amount)
    {
        if (amount < 0 || Stamp < amount) return false;
        Stamp -= amount;
        OnCurrencyChanged?.Invoke(Shell, Stamp);
        return true;
    }
}
