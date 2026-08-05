using Microsoft.EntityFrameworkCore;
using WlidCourt.Api.Models;
using WlidCourt.Data.DbContext;

namespace WlidCourt.Api.Services;

/// <summary>
/// 玩家业务：经验累计/等级计算（读 Config_PlayerLevel.json，等级 1~4 满级 1500 EXP）（文档 02 §2.3、03 §3.5）。
/// </summary>
public class PlayerService
{
    private const string PlayerLevelFile = "Config_PlayerLevel.json";

    private readonly WlidCourtDbContext _db;
    private readonly ConfigService _config;

    public PlayerService(WlidCourtDbContext db, ConfigService config)
    {
        _db = db;
        _config = config;
    }

    /// <summary>经验累计：写回 users.exp 并按配置表重算等级（经验不设上限存储，等级封顶 4）。</summary>
    public async Task<int> AddExpAsync(long userId, int exp)
    {
        if (exp <= 0)
        {
            throw new BusinessException(ApiCodes.BadRequest, "经验值必须大于 0");
        }

        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");

        user.Exp += exp;
        user.Level = CalcLevel(user.Exp);
        await _db.SaveChangesAsync();
        return user.Level;
    }

    /// <summary>玩家等级（返回当前存储等级）。</summary>
    public async Task<int> GetLevelAsync(long userId)
    {
        var user = await _db.Users.FindAsync(userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "用户不存在");
        return user.Level;
    }

    /// <summary>按累计经验查配置表算等级；配置表缺失时按文档默认曲线（0/200/600/1500）兜底。</summary>
    public int CalcLevel(int totalExp)
    {
        var rows = _config.GetRows<PlayerLevelConfigRow>(PlayerLevelFile);
        if (rows.Length == 0)
        {
            if (totalExp >= 1500) return 4;
            if (totalExp >= 600) return 3;
            if (totalExp >= 200) return 2;
            return 1;
        }

        var level = rows[0].Level;
        foreach (var row in rows)
        {
            if (totalExp >= row.TotalExp)
            {
                level = row.Level;
            }
        }
        return level;
    }
}
