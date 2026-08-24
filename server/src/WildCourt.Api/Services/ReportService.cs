using Microsoft.EntityFrameworkCore;
using WildCourt.Api.Models;
using WildCourt.Data.DbContext;
using WildCourt.Data.Entities;

namespace WildCourt.Api.Services;

/// <summary>
/// 举报 + 聊天封禁业务：玩家举报、查询聊天封禁状态、管理端举报审理/封禁聊天。
/// </summary>
public class ReportService
{
    private readonly WildCourtDbContext _db;
    private readonly ILogger<ReportService> _logger;

    public ReportService(WildCourtDbContext db, ILogger<ReportService> logger)
    {
        _db = db;
        _logger = logger;
    }

    /// <summary>玩家举报他人。同一对（举报人→被举报人）存在未处理举报时幂等拒绝。</summary>
    public async Task ReportAsync(long reporterUserId, long reportedUserId, string reason)
    {
        if (reporterUserId == reportedUserId)
        {
            throw new BusinessException(ApiCodes.BadRequest, "不能举报自己");
        }
        var reported = await _db.Users.FirstOrDefaultAsync(u => u.UserId == reportedUserId)
            ?? throw new BusinessException(ApiCodes.NotFound, "被举报玩家不存在");

        var trimmed = reason?.Trim() ?? string.Empty;
        if (trimmed.Length > 500)
        {
            throw new BusinessException(ApiCodes.BadRequest, "举报理由过长");
        }

        // 同一对玩家已有未处理举报则拒绝重复提交
        var existsPending = await _db.Reports.AnyAsync(r =>
            r.ReporterUserId == reporterUserId &&
            r.ReportedUserId == reportedUserId &&
            r.Status == 0);
        if (existsPending)
        {
            throw new BusinessException(ApiCodes.BadRequest, "您对该玩家已提交过举报，正在处理中");
        }

        _db.Reports.Add(new ReportEntity
        {
            ReporterUserId = reporterUserId,
            ReportedUserId = reportedUserId,
            Reason = trimmed,
            Status = 0,
            CreatedAt = DateTime.UtcNow,
        });
        await _db.SaveChangesAsync();
    }

    /// <summary>查询玩家聊天封禁状态：是否被封禁、截止时间、剩余天数。</summary>
    public async Task<ChatBanStatusDto> GetChatBanStatusAsync(long userId)
    {
        var user = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");

        var now = DateTime.UtcNow;
        var banUntil = user.ChatBanUntil;
        bool banned = banUntil.HasValue && banUntil.Value > now;

        return new ChatBanStatusDto
        {
            Banned = banned,
            BanUntil = banned ? banUntil : null,
            RemainDays = banned ? Math.Max(1, (int)Math.Ceiling((banUntil.Value - now).TotalDays)) : 0,
        };
    }

    /// <summary>封禁玩家聊天 N 天（days &lt;=0 则视为解封）。返回是否变更。</summary>
    public async Task<bool> SetChatBanAsync(long adminId, long userId, int days, string? reason = null)
    {
        var user = await _db.Users.FirstOrDefaultAsync(u => u.UserId == userId)
            ?? throw new BusinessException(ApiCodes.NotFound, "玩家不存在");

        if (days <= 0)
        {
            // 解封
            if (user.ChatBanUntil == null) return false;
            user.ChatBanUntil = null;
        }
        else
        {
            user.ChatBanUntil = DateTime.UtcNow.AddDays(days);
        }
        await _db.SaveChangesAsync();
        _logger.LogInformation("管理员 {AdminId} 设置玩家 {UserId} 聊天封禁 {Days} 天", adminId, userId, days);
        return true;
    }

    /// <summary>举报列表（可按状态筛选 + 分页）。</summary>
    public async Task<PagedResult<AdminReportListItemDto>> ListReportsAsync(int? status, int page, int pageSize)
    {
        var query = _db.Reports.AsQueryable();
        if (status.HasValue)
        {
            query = query.Where(r => r.Status == status.Value);
        }

        var total = await query.CountAsync();
        var items = await query
            .OrderByDescending(r => r.CreatedAt)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = items.SelectMany(r => new[] { r.ReporterUserId, r.ReportedUserId }).Distinct().ToList();
        var nickMap = await _db.Users
            .Where(u => userIds.Contains(u.UserId))
            .ToDictionaryAsync(u => u.UserId, u => u.Nickname);

        return new PagedResult<AdminReportListItemDto>
        {
            Items = items.Select(r => new AdminReportListItemDto
            {
                Id = r.Id,
                ReporterUserId = r.ReporterUserId,
                ReporterNickname = nickMap.TryGetValue(r.ReporterUserId, out var rn) ? rn : r.ReporterUserId.ToString(),
                ReportedUserId = r.ReportedUserId,
                ReportedNickname = nickMap.TryGetValue(r.ReportedUserId, out var dn) ? dn : r.ReportedUserId.ToString(),
                Reason = r.Reason,
                Status = r.Status,
                CreatedAt = r.CreatedAt,
            }).ToList(),
            Total = total,
        };
    }

    /// <summary>举报详情：基础信息 + 被举报人聊天封禁状态 + 两人全部聊天记录（时间升序）。</summary>
    public async Task<AdminReportDetailDto> GetReportDetailAsync(long id)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException(ApiCodes.NotFound, "举报不存在");

        var reporter = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == report.ReporterUserId);
        var reported = await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.UserId == report.ReportedUserId);

        // 两人之间的全部聊天记录
        var messages = await _db.ChatMessages
            .Where(m =>
                (m.FromUserId == report.ReporterUserId && m.ToUserId == report.ReportedUserId) ||
                (m.FromUserId == report.ReportedUserId && m.ToUserId == report.ReporterUserId))
            .OrderBy(m => m.Id)
            .ToListAsync();

        var chatBan = await GetChatBanStatusAsync(report.ReportedUserId);

        return new AdminReportDetailDto
        {
            Id = report.Id,
            ReporterUserId = report.ReporterUserId,
            ReporterNickname = reporter?.Nickname ?? report.ReporterUserId.ToString(),
            ReportedUserId = report.ReportedUserId,
            ReportedNickname = reported?.Nickname ?? report.ReportedUserId.ToString(),
            Reason = report.Reason,
            Status = report.Status,
            CreatedAt = report.CreatedAt,
            ChatBan = chatBan,
            ChatMessages = messages.Select(m => new ChatMessageDto
            {
                Id = m.Id,
                FromUserId = m.FromUserId,
                ToUserId = m.ToUserId,
                Content = m.Content,
                CreatedAt = new DateTimeOffset(System.DateTime.SpecifyKind(m.CreatedAt, System.DateTimeKind.Utc)).ToUnixTimeMilliseconds(),
            }).ToList(),
        };
    }

    /// <summary>处理举报：标记已处理；若 chatBanDays &gt; 0 则封禁被举报人聊天 N 天；否则仅标记处理（或按 chatBanDays=0 不封禁）。</summary>
    public async Task ResolveAsync(long adminId, long id, int chatBanDays, string? note)
    {
        var report = await _db.Reports.FirstOrDefaultAsync(r => r.Id == id)
            ?? throw new BusinessException(ApiCodes.NotFound, "举报不存在");

        if (chatBanDays > 0)
        {
            await SetChatBanAsync(adminId, report.ReportedUserId, chatBanDays, note);
        }

        report.Status = 1;
        report.HandledByAdminId = (int)adminId;
        report.HandledAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        _logger.LogInformation("管理员 {AdminId} 处理举报 {ReportId}，封禁聊天 {Days} 天", adminId, id, chatBanDays);
    }
}
