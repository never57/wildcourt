using System;
using UnityEngine;

/// <summary>
/// 玩家数据载体：等级 / 经验 / 省份 / 昵称。
/// 对应文档三 3.5 经验值系统、文档十六 Sheet17 PlayerLevel（等级上限 4，对应满级庭院进度）。
/// </summary>
public class player : MonoBehaviour
{
    public string Nickname;
    public int ProvinceId;     // 1~34（文档二）
    public int Level = 1;      // 玩家等级（上限 4，见 Sheet17）
    public int Exp;            // 当前等级累计经验
    public int YardProgress => Level; // 庭院解锁进度等级 1~4（文档三 3.5）

    // 钓鱼解锁：玩家等级达到 2 级（文档四 4.5）。
    public bool CanFish => Level >= 2;

    void Start()
    {
        // TODO: 从服务端拉取玩家档案。
    }

    void Update()
    {
        // 农田生长推进（运行时由 GameManager 统一驱动亦可）。
    }
}
