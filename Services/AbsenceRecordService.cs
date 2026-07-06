using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.ViewModels;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Services;

/// <summary>
/// 曠課紀錄服務實作（Phase 6）。
///
/// 業務規則來源：<c>docs/01-需求說明.md §3.5 / §4</c>
/// 與 <c>Models/Entities/AbsenceRecord.cs</c> 類別註釋。
///
/// 設計原則（與 <see cref="LeaveReportService"/> 一致）：
///   ‧ 純業務邏輯，不依賴 HttpContext；所有輸入透過參數傳入，方便單元測試
///   ‧ Controller 一行呼叫即可拿到「畫面所需的完整資料」（含下拉選項與總計）
///   ‧ 授權由 Controller 的 <c>[Authorize]</c> 決定；Service 只做「資料所有權」檢查
///     （<see cref="GetMineAsync"/> 強制以 <c>studentId</c> 為 WHERE 主條件）
/// </summary>
public class AbsenceRecordService : IAbsenceRecordService
{
    /// <summary>
    /// 學員角色名稱（與 <see cref="LeaveSystem.Models.Enums.UserRole.Student"/> 對應的 Role.Name）。
    /// 集中定義避免各處硬編字串（體例對齊 <see cref="LeaveReportService"/>）。
    /// </summary>
    private const string RoleNameStudent = "Student";

    private const string ErrorStudentNotFound = "找不到指定的學員，或該使用者不是學員身分。";
    private const string ErrorRecordNotFoundForUpdate = "找不到要更新的曠課紀錄。";
    private const string ErrorRecordNotFoundForDelete = "找不到要刪除的曠課紀錄。";

    private readonly AppDbContext _db;

    public AbsenceRecordService(AppDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<AbsenceRecordActionResult> CreateAsync(AbsenceRecordCreateInput input, int createdByUserId)
    {
        if (!await IsValidStudentAsync(input.StudentId))
        {
            return new AbsenceRecordActionResult(false, ErrorStudentNotFound);
        }

        var record = new AbsenceRecord
        {
            StudentId = input.StudentId,
            OccurredAt = input.OccurredAt,
            Hours = input.Hours,
            Note = NormalizeNote(input.Note),
            CreatedByUserId = createdByUserId,
            CreatedAt = DateTime.UtcNow
        };

        _db.AbsenceRecords.Add(record);
        await _db.SaveChangesAsync();

        return new AbsenceRecordActionResult(true, null, record.Id);
    }

    /// <inheritdoc />
    public async Task<AbsenceRecordActionResult> UpdateAsync(AbsenceRecordEditInput input)
    {
        var record = await _db.AbsenceRecords.FirstOrDefaultAsync(a => a.Id == input.Id);
        if (record is null)
        {
            return new AbsenceRecordActionResult(false, ErrorRecordNotFoundForUpdate);
        }

        if (!await IsValidStudentAsync(input.StudentId))
        {
            return new AbsenceRecordActionResult(false, ErrorStudentNotFound);
        }

        // 更新業務欄位；CreatedByUserId 與 CreatedAt 刻意保留，維持原稽核資訊
        record.StudentId = input.StudentId;
        record.OccurredAt = input.OccurredAt;
        record.Hours = input.Hours;
        record.Note = NormalizeNote(input.Note);

        await _db.SaveChangesAsync();
        return new AbsenceRecordActionResult(true, null, record.Id);
    }

    /// <inheritdoc />
    public async Task<AbsenceRecordActionResult> DeleteAsync(int id)
    {
        var record = await _db.AbsenceRecords.FirstOrDefaultAsync(a => a.Id == id);
        if (record is null)
        {
            return new AbsenceRecordActionResult(false, ErrorRecordNotFoundForDelete);
        }

        _db.AbsenceRecords.Remove(record);
        await _db.SaveChangesAsync();
        return new AbsenceRecordActionResult(true, null);
    }

    /// <inheritdoc />
    public async Task<AbsenceRecordAdminViewModel> GetForAdminAsync(AbsenceRecordAdminQuery query)
    {
        var q = _db.AbsenceRecords
            .AsNoTracking()
            .Include(a => a.Student)
                .ThenInclude(u => u.Cohort)
            .Include(a => a.CreatedByUser)
            .AsQueryable();

        if (query.CohortId.HasValue)
        {
            q = q.Where(a => a.Student.CohortId == query.CohortId.Value);
        }
        if (query.StudentId.HasValue)
        {
            q = q.Where(a => a.StudentId == query.StudentId.Value);
        }
        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            q = q.Where(a => a.OccurredAt >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = NormalizeDateTo(query.DateTo.Value);
            q = q.Where(a => a.OccurredAt <= to);
        }

        var rows = await q
            .OrderByDescending(a => a.OccurredAt)
            .ToListAsync();

        var items = rows
            .Select(a => new AbsenceRecordListItem
            {
                Id = a.Id,
                StudentDisplayName = a.Student.DisplayName,
                CohortName = a.Student.Cohort?.Name,
                OccurredAt = a.OccurredAt,
                Hours = a.Hours,
                Note = a.Note,
                CreatedByDisplayName = a.CreatedByUser.DisplayName,
                CreatedAt = a.CreatedAt
            })
            .ToList();

        return new AbsenceRecordAdminViewModel
        {
            Query = query,
            Items = items,
            TotalHours = items.Sum(i => i.Hours),
            CohortOptions = await LoadCohortOptionsAsync(),
            StudentOptions = await GetStudentOptionsAsync()
        };
    }

    /// <inheritdoc />
    public async Task<MyAbsenceRecordViewModel> GetMineAsync(int studentId, MyAbsenceRecordQuery query)
    {
        // 主 WHERE：一律以 studentId 篩選（安全性關鍵，即便 query 被竄改也不會漏資料）
        var q = _db.AbsenceRecords
            .AsNoTracking()
            .Where(a => a.StudentId == studentId);

        if (query.DateFrom.HasValue)
        {
            var from = query.DateFrom.Value;
            q = q.Where(a => a.OccurredAt >= from);
        }
        if (query.DateTo.HasValue)
        {
            var to = NormalizeDateTo(query.DateTo.Value);
            q = q.Where(a => a.OccurredAt <= to);
        }

        var items = await q
            .OrderByDescending(a => a.OccurredAt)
            .Select(a => new MyAbsenceRecordListItem
            {
                Id = a.Id,
                OccurredAt = a.OccurredAt,
                Hours = a.Hours,
                Note = a.Note,
                CreatedAt = a.CreatedAt
            })
            .ToListAsync();

        // 終身總計：**刻意不套用日期篩選**，讓學員每次進頁面都看到自我警惕的完整數字
        var totalHours = await _db.AbsenceRecords
            .AsNoTracking()
            .Where(a => a.StudentId == studentId)
            .SumAsync(a => (decimal?)a.Hours) ?? 0m;

        return new MyAbsenceRecordViewModel
        {
            Query = query,
            Items = items,
            TotalHours = totalHours
        };
    }

    /// <inheritdoc />
    public async Task<AbsenceRecordEditInput?> GetForEditAsync(int id)
    {
        var record = await _db.AbsenceRecords
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id);

        if (record is null) return null;

        return new AbsenceRecordEditInput
        {
            Id = record.Id,
            StudentId = record.StudentId,
            OccurredAt = record.OccurredAt,
            Hours = record.Hours,
            Note = record.Note,
            StudentOptions = await GetStudentOptionsAsync()
        };
    }

    // ═════════════════════════════════════════════════════════════════
    // 私有 Helpers
    // ═════════════════════════════════════════════════════════════════

    /// <summary>
    /// 備註正規化：空白/全空白字串一律轉為 <c>null</c>；否則 <c>Trim()</c>。
    /// </summary>
    private static string? NormalizeNote(string? note)
    {
        if (string.IsNullOrWhiteSpace(note)) return null;
        return note.Trim();
    }

    /// <summary>
    /// 日期迄正規化：若時間為 00:00:00（<c>&lt;input type="date"&gt;</c> 只帶日期時的預設），
    /// 補到當日最後一刻（<c>23:59:59.9999999</c>）以「含當天」為語意；否則沿用使用者給的時間。
    /// 體例對齊 <see cref="LeaveReportService.GetAdminReportAsync"/>。
    /// </summary>
    private static DateTime NormalizeDateTo(DateTime dateTo)
    {
        return dateTo.TimeOfDay == TimeSpan.Zero
            ? dateTo.Date.AddDays(1).AddTicks(-1)
            : dateTo;
    }

    /// <summary>
    /// 檢查該 UserId 是否存在，且擁有 Student 角色。
    /// </summary>
    private Task<bool> IsValidStudentAsync(int userId)
    {
        return _db.UserAccounts
            .AsNoTracking()
            .AnyAsync(u => u.Id == userId
                && u.UserRoles.Any(ur => ur.Role.Name == RoleNameStudent));
    }

    /// <summary>
    /// 載入班期下拉選項（依名稱排序）。
    /// </summary>
    private Task<List<CohortOption>> LoadCohortOptionsAsync()
    {
        return _db.Cohorts
            .AsNoTracking()
            .OrderBy(c => c.Name)
            .Select(c => new CohortOption { Id = c.Id, Name = c.Name })
            .ToListAsync();
    }

    /// <summary>
    /// 載入學員下拉選項（所有擁有 Student 角色的使用者，含停用者，
    /// 因為歷史紀錄可能綁在已停用的學員身上）。
    /// 也對外公開，讓 Controller 在 <c>Create GET</c> 或 <c>ModelState</c>
    /// 驗證失敗需要重補下拉時可直接呼叫。
    /// </summary>
    public Task<List<UserOption>> GetStudentOptionsAsync()
    {
        return _db.UserAccounts
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
    }
}
