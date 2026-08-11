using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 操作审计日志查询：分页检索 operation_logs，支持按管理员/操作名/关键词/时间/状态码筛选。
/// 写入由 <c>AdminAuditMiddleware</c> 自动完成。
/// </summary>
public class AdminAuditService
{
    private readonly WildCourtDbContext _db;

    public AdminAuditService(WildCourtDbContext db)
    {
        _db = db;
    }

    public async Task<PagedResult<AdminOperationLogDto>> QueryAsync(AuditLogQueryRequest query)
    {
        var q = _db.AdminOperationLogs.AsNoTracking();

        if (query.AdminId is long adminId)
        {
            q = q.Where(x => x.AdminId == adminId);
        }

        if (!string.IsNullOrWhiteSpace(query.Action))
        {
            q = q.Where(x => x.Action == query.Action);
        }

        if (!string.IsNullOrWhiteSpace(query.Keyword))
        {
            var kw = query.Keyword.Trim();
            q = q.Where(x => x.AdminName.Contains(kw) || x.Action.Contains(kw) || x.Path.Contains(kw));
        }

        if (query.StartTime is DateTime start)
        {
            q = q.Where(x => x.CreatedAt >= start);
        }

        if (query.EndTime is DateTime end)
        {
            q = q.Where(x => x.CreatedAt <= end);
        }

        if (query.StatusCode is int code)
        {
            q = q.Where(x => x.StatusCode == code);
        }

        var total = await q.LongCountAsync();
        var items = await q
            .OrderByDescending(x => x.LogId)
            .Skip((query.Page - 1) * query.PageSize)
            .Take(query.PageSize)
            .Select(x => new AdminOperationLogDto
            {
                LogId = x.LogId,
                AdminId = x.AdminId,
                AdminName = x.AdminName,
                Action = x.Action,
                Method = x.Method,
                Path = x.Path,
                Details = x.Details,
                IpAddress = x.IpAddress,
                StatusCode = x.StatusCode,
                DurationMs = x.DurationMs,
                CreatedAt = x.CreatedAt,
            })
            .ToListAsync();

        return new PagedResult<AdminOperationLogDto> { Items = items, Total = total };
    }

    /// <summary>返回所有出现过的操作名（按次数倒序，用于前端筛选下拉）。</summary>
    public async Task<List<string>> GetActionsAsync()
    {
        return await _db.AdminOperationLogs.AsNoTracking()
            .GroupBy(x => x.Action)
            .OrderByDescending(g => g.Count())
            .Select(g => g.Key)
            .ToListAsync();
    }
}
