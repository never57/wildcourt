namespace WildCourt.Data.Entities;

/// <summary>
/// 每日签到记录表（表名 sign_in_records，文档 15.3 核心表之一）。
/// 每日一条（user_id + sign_date 唯一），用于连续签到统计与月里程碑累计判断。
/// </summary>
public class SignInRecordEntity
{
    public long RecordId { get; set; }

    public long UserId { get; set; }

    /// <summary>签到日期（UTC+8 自然日，存该日零点）。</summary>
    public DateTime SignDate { get; set; }

    public DateTime CreatedAt { get; set; }
}
