using System;
using UnityEngine;

namespace WildCourt.Core
{
    /// <summary>Config_PlayerLevel.json（Sheet17）单行。</summary>
    [Serializable]
    public class PlayerLevelConfigRow
    {
        public int level;        // 玩家等级（1~4）
        public int totalExp;     // 升到该等级所需累计经验
        public int yardProgress; // 对应庭院解锁进度（1~4，见 Sheet16）
        public string note;      // 备注（可选）
    }

    /// <summary>
    /// 玩家经验/等级（文档 01 §1.6、03 §3.5、16 Sheet17）：
    /// 读 Config_PlayerLevel.json，等级 1~4，满级累计 1500 EXP。
    /// <para>服务端 PlayerService 为权威，本类仅做客户端本地预估与 UI 展示，
    /// 服务端快照返回后调用 <see cref="SetExp"/> 对齐。</para>
    /// </summary>
    public class PlayerLevel
    {
        public const string TableName = "PlayerLevel";

        public int Level { get; private set; } = 1;
        public int Exp { get; private set; }
        public int MaxLevel { get; private set; } = 4;

        private PlayerLevelConfigRow[] _rows;

        public void Init()
        {
            _rows = ConfigMgr.Instance.GetTable<PlayerLevelConfigRow>(TableName);
            if (_rows.Length > 0)
            {
                MaxLevel = _rows[_rows.Length - 1].level;
                Level = _rows[0].level;
                Exp = 0;
            }
            else
            {
                Debug.LogWarning($"[PlayerLevel] 缺少配置表 {TableName}，使用默认等级曲线（1~4）");
            }
        }

        /// <summary>按累计经验重算等级（服务端快照同步后调用）。</summary>
        public void SetExp(int totalExp)
        {
            Exp = Mathf.Max(0, totalExp);
            Level = CalcLevel(Exp);
            EventBus.Publish(new PlayerExpChangedEvent { Level = Level, Exp = Exp });
        }

        /// <summary>本地预估：累加经验并广播变化事件。</summary>
        public void AddExp(int exp)
        {
            SetExp(Exp + exp);
        }

        public int CalcLevel(int totalExp)
        {
            int lv = 1;
            if (_rows == null) return lv;
            foreach (PlayerLevelConfigRow row in _rows)
            {
                if (totalExp >= row.totalExp) lv = row.level;
            }
            return lv;
        }

        /// <summary>升到下一级所需累计经验；已满级返回 -1。</summary>
        public int GetNextLevelTotalExp()
        {
            if (_rows == null) return -1;
            foreach (PlayerLevelConfigRow row in _rows)
            {
                if (row.level == Level + 1) return row.totalExp;
            }
            return -1;
        }
    }
}
