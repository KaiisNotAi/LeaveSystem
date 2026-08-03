using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Services;

/// <summary>
/// 紀錄查詢與時數統計服務實作（Phase 5）。
///
/// 業務規則見 <c>docs/04-請假與簽核流程.md §7</c>：
///   ‧ 學員查詢：以 <paramref name="studentId"/> 為 WHERE 主條件，Controller 從 Cookie 帶入。
///   ‧ 累計時數：只算 <see cref="LeaveStatus.Approved"/>（與 Phase 3 班期上限口徑一致）。
///   ‧ 行政報表：分組維度 Cohort × Student × LeaveType × YearMonth，跨月請假整筆算開始月。
///
/// 設計原則：
///   ‧ 純業務邏輯，不依賴 HttpContext。
///   ‧ 回傳「該畫面所需的完整資料」（含下拉選項），Controller 一行呼叫即可。
/// </summary>
public class LeaveReportService : ILeaveReportService
{
    /// <summary>
    /// 學員角色名稱（與 <see cref="UserRole.Student"/> 對應的 Role.Name）。
    /// 集中定義避免各處硬編字串。
    /// </summary>
    private const string RoleNameStudent = "Student";

    private readonly AppDbContext _db;

    public LeaveReportService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<MyLeaveRequestListItem>> GetMyRequestsAsync(int studentId, MyLeaveRequestQuery query)
    {
        // 主 WHERE：一律以 studentId 篩選（安全性關鍵，即使 query 被竄改也不會漏資料）
        var q = _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.LeaveType)
            .Include(r => r.Steps)
            .Where(r => r.StudentId == studentId);

        // 選填條件：以「AND」串接
        if (query.Status.HasValue)
        {
            q = q.Where(r => r.Status == query.Status.Value);
        }
        if (query.LeaveTypeId.HasValue)
        {
            q = q.Where(r => r.LeaveTypeId == query.LeaveTypeId.Value);
        }
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            q = q.Where(r => r.StartAt >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value;
            q = q.Where(r => r.StartAt <= to);
        }

        // 投影：清單頁需要 Reason / RejectReason
        // RejectReason 只有 Status=Rejected 才會有值，且來自 Decision=Rejected 的 Step.Comment
        // （Phase 4 規則：任一級駁回整張單終止，因此最多一筆 Rejected Step）
        var list = await q
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync();

        return list
            .Select(r => new MyLeaveRequestListItem
            {
                Id = r.Id,
                LeaveTypeName = r.LeaveType.Name,
                StartAt = r.StartAt,
                EndAt = r.EndAt,
                TotalHours = r.TotalHours,
                Status = r.Status,
                CurrentLevel = r.CurrentLevel,
                CreatedAt = r.CreatedAt,
                Reason = r.Reason,
                RejectReason = r.Status == LeaveStatus.Rejected
                    ? r.Steps
                        .Where(s => s.Decision == ApprovalDecision.Rejected)
                        .Select(s => s.Comment)
                        .FirstOrDefault()
                    : null
            })
            .ToList();
    }

    /// <inheritdoc />
    public async Task<MyLeaveSummaryViewModel> GetMyLeaveTypeSummaryAsync(int studentId)
    {
        // 先取學員（含班期），Cohort 可能為 null（尚未指派班期）
        var student = await _db.UserAccounts
            .AsNoTracking()
            .Include(u => u.Cohort)
            .FirstOrDefaultAsync(u => u.Id == studentId);

        // 只累計 Approved（與 Phase 3 班期上限口徑一致）
        // 撈完整實體到記憶體，方便同時做 group + 依 LeaveType.SortOrder 排序
        var approved = await _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.LeaveType)
            .Where(r => r.StudentId == studentId && r.Status == LeaveStatus.Approved)
            .ToListAsync();

        var items = approved
            .GroupBy(r => new { r.LeaveTypeId, r.LeaveType.Name, r.LeaveType.SortOrder })
            .OrderBy(g => g.Key.SortOrder)
            .ThenBy(g => g.Key.LeaveTypeId)
            .Select(g => new MyLeaveTypeSummaryItem
            {
                LeaveTypeName = g.Key.Name,
                TotalHours = g.Sum(r => r.TotalHours),
                RequestCount = g.Count()
            })
            .ToList();

        return new MyLeaveSummaryViewModel
        {
            Items = items,
            TotalApprovedHours = items.Sum(i => i.TotalHours),
            CohortLeaveLimitHours = student?.Cohort?.LeaveLimitHours,
            CohortName = student?.Cohort?.Name
        };
    }

    /// <inheritdoc />
    public async Task<AdminReportViewModel> GetAdminReportAsync(AdminReportQuery query)
    {
        // 撈原始資料（僅 Approved），連班期 / 學員 / 假別一起讀進來
        // 之後在記憶體做 group + 排序，避免 GroupBy 在不同 DB provider 下的翻譯差異
        var q = _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.LeaveType)
            .Include(r => r.Student)
                .ThenInclude(u => u.Cohort)
            .Where(r => r.Status == LeaveStatus.Approved);

        if (query.CohortId.HasValue)
        {
            q = q.Where(r => r.Student.CohortId == query.CohortId.Value);
        }
        if (query.StudentId.HasValue)
        {
            q = q.Where(r => r.StudentId == query.StudentId.Value);
        }
        if (query.LeaveTypeId.HasValue)
        {
            q = q.Where(r => r.LeaveTypeId == query.LeaveTypeId.Value);
        }

        // 日期篩選（比對 StartAt）
        // DateTo 若時間為 00:00:00（<input type="date"> 只帶日期時的預設），
        // 補到當日 23:59:59.9999 以「含當天」為語意；否則沿用使用者給的時間。
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            q = q.Where(r => r.StartAt >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.TimeOfDay == TimeSpan.Zero
                ? query.DateTo.Value.Date.AddDays(1).AddTicks(-1)
                : query.DateTo.Value;
            q = q.Where(r => r.StartAt <= to);
        }

        var rawRequests = await q.ToListAsync();

        // 記憶體端做分組
        // 跨月請假整筆算開始月：以 StartAt.Year / StartAt.Month 為分組鍵（docs/04 §7.3）
        var rows = rawRequests
            .Where(r => r.Student.Cohort is not null) // 沒有班期的學員不進報表
            .GroupBy(r => new
            {
                CohortId = r.Student.CohortId!.Value,
                CohortName = r.Student.Cohort!.Name,
                r.StudentId,
                StudentDisplayName = r.Student.DisplayName,
                r.LeaveTypeId,
                LeaveTypeName = r.LeaveType.Name,
                Year = r.StartAt.Year,
                Month = r.StartAt.Month
            })
            .Select(g => new AdminReportRow
            {
                CohortName = g.Key.CohortName,
                StudentDisplayName = g.Key.StudentDisplayName,
                LeaveTypeName = g.Key.LeaveTypeName,
                Year = g.Key.Year,
                Month = g.Key.Month,
                TotalHours = g.Sum(r => r.TotalHours),
                RequestCount = g.Count()
            })
            .OrderBy(r => r.CohortName)
            .ThenBy(r => r.StudentDisplayName)
            .ThenBy(r => r.LeaveTypeName)
            .ThenBy(r => r.Year)
            .ThenBy(r => r.Month)
            .ToList();

        // ─── 下拉選項（Controller 直接綁 Razor 表單） ───
        var cohortOptions = await _db.Cohorts
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CohortOption { Id = c.Id, Name = c.Name })
            .ToListAsync();

        var studentOptions = await _db.UserAccounts
            .AsNoTracking()
            .Where(u => u.UserRoles.Any(ur => ur.Role.Name == RoleNameStudent))
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserOption
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Username = u.Username
            })
            .ToListAsync();

        var leaveTypeOptions = await _db.LeaveTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .Select(t => new LeaveTypeOption { Id = t.Id, Name = t.Name })
            .ToListAsync();

        return new AdminReportViewModel
        {
            Query = query,
            Rows = rows,
            CohortOptions = cohortOptions,
            StudentOptions = studentOptions,
            LeaveTypeOptions = leaveTypeOptions
        };
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<AdminLeaveDetailRow>> GetLeaveDetailsForAdminAsync(AdminExportQuery query)
    {
        ArgumentNullException.ThrowIfNull(query);
        if (!query.CohortId.HasValue)
        {
            // CohortId 是必填；防呆：Controller 應已擋下，Service 再擋一次避免整表撈出。
            return Array.Empty<AdminLeaveDetailRow>();
        }

        var q = _db.LeaveRequests
            .AsNoTracking()
            .Include(r => r.LeaveType)
            .Include(r => r.Student)
                .ThenInclude(u => u.Cohort)
            .Where(r => r.Student.CohortId == query.CohortId.Value);

        if (query.StudentId.HasValue)
        {
            q = q.Where(r => r.StudentId == query.StudentId.Value);
        }
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            q = q.Where(r => r.StartAt >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = query.DateTo.Value.TimeOfDay == TimeSpan.Zero
                ? query.DateTo.Value.Date.AddDays(1).AddTicks(-1)
                : query.DateTo.Value;
            q = q.Where(r => r.StartAt <= to);
        }

        var rows = await q
            .OrderByDescending(r => r.StartAt)
            .Select(r => new AdminLeaveDetailRow
            {
                Id = r.Id,
                CreatedAt = r.CreatedAt,
                CohortName = r.Student.Cohort != null ? r.Student.Cohort.Name : string.Empty,
                StudentDisplayName = r.Student.DisplayName,
                LeaveTypeName = r.LeaveType.Name,
                StartAt = r.StartAt,
                EndAt = r.EndAt,
                TotalHours = r.TotalHours,
                Status = r.Status,
                CurrentLevel = r.CurrentLevel,
                Reason = r.Reason
            })
            .ToListAsync();

        return rows;
    }
}
