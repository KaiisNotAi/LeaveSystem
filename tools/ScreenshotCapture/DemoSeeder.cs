using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.Enums;
using Microsoft.EntityFrameworkCore;
using UserRoleEntity = LeaveSystem.Models.Entities.UserRole;
using UserRoleEnum = LeaveSystem.Models.Enums.UserRole;

namespace LeaveSystem.Tools.ScreenshotCapture;

/// <summary>
/// 專題報告截圖用的示範資料。
///
/// 設計原則：
///   1. **純新增**——所有資料列都掛在 <see cref="DemoUserPrefix"/> 帳號與示範班期底下，
///      不修改、不刪除任何既有資料列（既有帳號的密碼雜湊尤其不動）。
///   2. **冪等**——<see cref="SeedAsync"/> 會先呼叫 <see cref="RollbackAsync"/> 清乾淨再重建，
///      因此可以重複執行而不會越長越多。
///   3. **可回滾**——<see cref="RollbackAsync"/> 以帳號前綴反查，一次移除全部示範資料。
/// </summary>
public static class DemoSeeder
{
    /// <summary>示範帳號的統一前綴，回滾時就是靠這個前綴反查。</summary>
    public const string DemoUserPrefix = "demo.";

    /// <summary>示範帳號的統一密碼（僅存在於本機截圖流程）。</summary>
    public const string DemoPassword = "Demo@123";

    public const string DemoCohortName = "115-1 示範班（報告截圖用）";

    // 截圖流程會用到的帳號名稱
    public const string Student1 = DemoUserPrefix + "stu01";
    public const string Student2 = DemoUserPrefix + "stu02";
    public const string Student3 = DemoUserPrefix + "stu03";
    public const string Tutor = DemoUserPrefix + "tutor";
    public const string SectionChief = DemoUserPrefix + "chief";
    public const string BranchDirector = DemoUserPrefix + "director";
    public const string Staff = DemoUserPrefix + "staff";
    public const string Admin = DemoUserPrefix + "admin";

    /// <summary>
    /// 建立示範資料，回傳截圖流程需要的識別碼（班期 Id、待簽核假單 Id 等）。
    /// </summary>
    public static async Task<DemoDataIds> SeedAsync(AppDbContext db)
    {
        // 先清乾淨，確保重跑結果一致
        await RollbackAsync(db);

        var now = DateTime.UtcNow;

        // ── 1. 簽核人與行政帳號（不屬於任何班期）────────────────────────
        var tutor = NewUser(Tutor, "方怡君（導師）", "tutor@demo.local", now);
        var chief = NewUser(SectionChief, "張建成（科長）", "chief@demo.local", now);
        var director = NewUser(BranchDirector, "李文彬（分署長）", "director@demo.local", now);
        var staff = NewUser(Staff, "吳雅婷（行政）", "staff@demo.local", now);
        var admin = NewUser(Admin, "示範管理員", "admin@demo.local", now);

        db.UserAccounts.AddRange(tutor, chief, director, staff, admin);
        await db.SaveChangesAsync();

        AddRole(db, tutor, UserRoleEnum.Tutor);
        AddRole(db, chief, UserRoleEnum.SectionChief);
        AddRole(db, director, UserRoleEnum.BranchDirector);
        AddRole(db, staff, UserRoleEnum.Staff);
        AddRole(db, admin, UserRoleEnum.Admin);
        await db.SaveChangesAsync();

        // ── 2. 示範班期（三級簽核人都指派好）─────────────────────────────
        var cohort = new Cohort
        {
            Name = DemoCohortName,
            StartDate = new DateTime(2026, 7, 1),
            EndDate = new DateTime(2027, 1, 1),
            TotalHours = 900,
            LeaveLimitPercent = 10,     // 上限 90 小時
            TutorUserId = tutor.Id,
            SectionChiefUserId = chief.Id,
            BranchDirectorUserId = director.Id
        };
        db.Cohorts.Add(cohort);
        await db.SaveChangesAsync();

        // ── 3. 學員（掛在示範班期底下）──────────────────────────────────
        var stu1 = NewUser(Student1, "王小明", "stu01@demo.local", now, cohort.Id);
        var stu2 = NewUser(Student2, "林佳蓉", "stu02@demo.local", now, cohort.Id);
        var stu3 = NewUser(Student3, "黃志豪", "stu03@demo.local", now, cohort.Id);
        db.UserAccounts.AddRange(stu1, stu2, stu3);
        await db.SaveChangesAsync();

        AddRole(db, stu1, UserRoleEnum.Student);
        AddRole(db, stu2, UserRoleEnum.Student);
        AddRole(db, stu3, UserRoleEnum.Student);
        await db.SaveChangesAsync();

        // ── 4. 假別 Id（沿用系統既有的四種假別）──────────────────────────
        var types = await db.LeaveTypes.AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ToDictionaryAsync(t => t.Code, t => t.Id);

        int Sick = types["SICK"], Personal = types["PERSONAL"],
            Bereavement = types["BEREAVEMENT"], Official = types["OFFICIAL"];

        // ── 5. 各種狀態的請假單 ─────────────────────────────────────────
        // 時數刻意配合 ApprovalRules：0~8→導師；8.01~24→導師+科長；24.01~→導師+科長+分署長
        var ids = new DemoDataIds { CohortId = cohort.Id };

        // 已核准（8h，單關）
        Add(db, stu1, Sick, D(8, 3, 8), D(8, 3, 17), "感冒發燒，已就醫並取得診斷證明。",
            LeaveStatus.Approved, 1,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "已確認診斷證明，准假。", D(8, 2, 16)));

        // 已核准（16h，兩關）
        Add(db, stu1, Personal, D(8, 10, 8), D(8, 11, 17), "家中長輩住院需陪同就醫兩日。",
            LeaveStatus.Approved, 2,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "情況屬實，同意。", D(8, 7, 10)),
            Step(2, "SectionChief", ApprovalDecision.Approved, chief.Id, "同意，請自行補課。", D(8, 7, 15)));

        // 待簽核（停在導師這關）→ 會出現在 demo.tutor 的待辦清單
        ids.PendingTutorRequestId = Add(db, stu1, Sick, D(8, 24, 8), D(8, 24, 12), "牙科手術，需半日休養。",
            LeaveStatus.Pending, 1,
            Step(1, "Tutor", ApprovalDecision.Pending, null, null, null));

        // 已駁回
        Add(db, stu1, Personal, D(7, 20, 13), D(7, 20, 17), "朋友聚會。",
            LeaveStatus.Rejected, 1,
            Step(1, "Tutor", ApprovalDecision.Rejected, tutor.Id, "事由非急迫且未附證明，請假不予核准。", D(7, 19, 9)));

        // 已取消（學員自行撤單）
        Add(db, stu1, Official, D(9, 1, 8), D(9, 1, 17), "原訂參加校外研習，主辦單位取消。",
            LeaveStatus.Cancelled, 1,
            Step(1, "Tutor", ApprovalDecision.Pending, null, null, null));

        // 待簽核（導師關）
        Add(db, stu2, Sick, D(8, 25, 13), D(8, 25, 17), "腸胃不適，下午需就醫。",
            LeaveStatus.Pending, 1,
            Step(1, "Tutor", ApprovalDecision.Pending, null, null, null));

        // 待簽核（導師已過，停在科長關）→ 出現在 demo.chief 的待辦
        Add(db, stu2, Personal, D(8, 26, 8), D(8, 28, 17), "返鄉辦理戶籍與役政相關手續，共三日。",
            LeaveStatus.Pending, 2,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "已核對相關文件，轉呈科長。", D(8, 20, 11)),
            Step(2, "SectionChief", ApprovalDecision.Pending, null, null, null));

        // 已核准（4h）
        Add(db, stu2, Sick, D(8, 5, 13), D(8, 5, 17), "定期回診。",
            LeaveStatus.Approved, 1,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "准假。", D(8, 4, 14)));

        // 待簽核（導師關）
        Add(db, stu3, Personal, D(8, 27, 8), D(8, 27, 12), "監理站辦理駕照換發。",
            LeaveStatus.Pending, 1,
            Step(1, "Tutor", ApprovalDecision.Pending, null, null, null));

        // 待簽核（前兩關已過，停在分署長關）→ 出現在 demo.director 的待辦
        Add(db, stu3, Bereavement, D(9, 7, 8), D(9, 11, 17), "祖父辭世，需返鄉處理喪葬事宜，共五日。",
            LeaveStatus.Pending, 3,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "已核對訃聞，轉呈。", D(9, 1, 9)),
            Step(2, "SectionChief", ApprovalDecision.Approved, chief.Id, "同意，續呈分署長核示。", D(9, 1, 14)),
            Step(3, "BranchDirector", ApprovalDecision.Pending, null, null, null));

        // 已核准（4h）
        Add(db, stu3, Official, D(8, 12, 8), D(8, 12, 12), "代表班級參加職訓成果發表會。",
            LeaveStatus.Approved, 1,
            Step(1, "Tutor", ApprovalDecision.Approved, tutor.Id, "公假登記，時數不計入請假上限。", D(8, 8, 9)));

        await db.SaveChangesAsync();

        // ── 6. 曠課紀錄（行政建立）──────────────────────────────────────
        db.AbsenceRecords.AddRange(
            NewAbsence(stu1.Id, staff.Id, D(8, 6, 8), 2m, "第一、二節未到，事後未補請假單。"),
            NewAbsence(stu1.Id, staff.Id, D(8, 18, 13), 4m, "下午全時段未到，電話聯繫未果。"),
            NewAbsence(stu2.Id, staff.Id, D(8, 13, 8), 1m, "遲到逾一小時，依規定以曠課一小時計。"),
            NewAbsence(stu3.Id, staff.Id, D(7, 29, 8), 8m, "全日未到課且未請假。"),
            NewAbsence(stu3.Id, staff.Id, D(8, 14, 15), 2m, "第七、八節離場未歸。"));

        // ── 7. 站內通知（含未讀，讓導覽列 🔔 徽章有數字）────────────────
        db.Notifications.AddRange(
            NewNotification(stu1.Id, "請假單已核准",
                "您 8/3 的病假申請（8 小時）已完成所有簽核。", "/LeaveRequests/Index", true, D(8, 2, 16)),
            NewNotification(stu1.Id, "請假單已駁回",
                "您 7/20 的事假申請被導師駁回：事由非急迫且未附證明，請假不予核准。", "/LeaveRequests/Index", false, D(7, 19, 9)),
            NewNotification(stu1.Id, "請假單送出成功",
                "您 8/24 的病假申請（4 小時）已送出，正等待導師簽核。", "/LeaveRequests/Index", false, D(8, 20, 9)),
            NewNotification(tutor.Id, "有新的假單待您簽核",
                "王小明 送出 8/24 病假（4 小時），請您簽核。", "/Approvals/Details/" + ids.PendingTutorRequestId, false, D(8, 20, 9)),
            NewNotification(chief.Id, "有新的假單待您簽核",
                "林佳蓉 的事假（24 小時）已由導師核准，請您接續簽核。", "/Approvals/Index", false, D(8, 20, 11)),
            NewNotification(director.Id, "有新的假單待您簽核",
                "黃志豪 的喪假（40 小時）已完成前兩關簽核，請您核示。", "/Approvals/Index", false, D(9, 1, 14)));

        await db.SaveChangesAsync();

        return ids;
    }

    /// <summary>
    /// 移除全部示範資料（依 <see cref="DemoUserPrefix"/> 帳號與示範班期反查）。
    /// 既有的真實資料完全不受影響。
    /// </summary>
    public static async Task RollbackAsync(AppDbContext db)
    {
        var demoUserIds = await db.UserAccounts
            .Where(u => u.Username.StartsWith(DemoUserPrefix))
            .Select(u => u.Id)
            .ToListAsync();

        if (demoUserIds.Count > 0)
        {
            // 有外鍵相依，刪除順序：Steps → Requests → 其餘子表 → UserRoles → Users
            var requestIds = await db.LeaveRequests
                .Where(r => demoUserIds.Contains(r.StudentId))
                .Select(r => r.Id)
                .ToListAsync();

            await db.LeaveRequestSteps
                .Where(s => requestIds.Contains(s.LeaveRequestId) || (s.ApproverUserId != null && demoUserIds.Contains(s.ApproverUserId.Value)))
                .ExecuteDeleteAsync();

            await db.LeaveRequests.Where(r => requestIds.Contains(r.Id)).ExecuteDeleteAsync();

            await db.AbsenceRecords
                .Where(a => demoUserIds.Contains(a.StudentId) || demoUserIds.Contains(a.CreatedByUserId))
                .ExecuteDeleteAsync();

            await db.Notifications.Where(n => demoUserIds.Contains(n.UserId)).ExecuteDeleteAsync();
            await db.UserRoles.Where(ur => demoUserIds.Contains(ur.UserId)).ExecuteDeleteAsync();

            // 先把示範班期上的簽核人指派解除，才能刪掉簽核人帳號
            await db.Cohorts
                .Where(c => c.Name == DemoCohortName)
                .ExecuteUpdateAsync(s => s
                    .SetProperty(c => c.TutorUserId, (int?)null)
                    .SetProperty(c => c.SectionChiefUserId, (int?)null)
                    .SetProperty(c => c.BranchDirectorUserId, (int?)null));

            // 學員帳號要先脫離班期，班期才刪得掉
            await db.UserAccounts
                .Where(u => demoUserIds.Contains(u.Id))
                .ExecuteUpdateAsync(s => s.SetProperty(u => u.CohortId, (int?)null));

            await db.UserAccounts.Where(u => demoUserIds.Contains(u.Id)).ExecuteDeleteAsync();
        }

        await db.Cohorts.Where(c => c.Name == DemoCohortName).ExecuteDeleteAsync();
    }

    // ─────────────────────────────────────────────────────────────
    // 小工具
    // ─────────────────────────────────────────────────────────────

    /// <summary>示範資料一律落在 2026 年，讓截圖裡的日期彼此連貫。</summary>
    private static DateTime D(int month, int day, int hour) => new(2026, month, day, hour, 0, 0);

    private static UserAccount NewUser(string username, string displayName, string email, DateTime now, int? cohortId = null)
        => new()
        {
            Username = username,
            DisplayName = displayName,
            Email = email,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DemoPassword),
            CohortId = cohortId,
            IsActive = true,
            CreatedAt = now,
            UpdatedAt = now
        };

    private static void AddRole(AppDbContext db, UserAccount user, UserRoleEnum role)
        => db.UserRoles.Add(new UserRoleEntity { UserId = user.Id, RoleId = (int)role });

    private static AbsenceRecord NewAbsence(int studentId, int createdBy, DateTime occurredAt, decimal hours, string note)
        => new()
        {
            StudentId = studentId,
            CreatedByUserId = createdBy,
            OccurredAt = occurredAt,
            Hours = hours,
            Note = note,
            CreatedAt = occurredAt
        };

    private static Notification NewNotification(int userId, string title, string message, string url, bool isRead, DateTime createdAt)
        => new()
        {
            UserId = userId,
            Title = title,
            Message = message,
            Url = url,
            IsRead = isRead,
            CreatedAt = createdAt,
            ReadAt = isRead ? createdAt.AddHours(1) : null
        };

    private static (int Level, string Role, ApprovalDecision Decision, int? ApproverUserId, string? Comment, DateTime? DecidedAt)
        Step(int level, string role, ApprovalDecision decision, int? approverUserId, string? comment, DateTime? decidedAt)
        => (level, role, decision, approverUserId, comment, decidedAt);

    /// <summary>建立一張請假單與其簽核步驟，時數以 <see cref="LeaveCalculatorRules"/> 的上課時段規則推算。</summary>
    private static int Add(
        AppDbContext db,
        UserAccount student,
        int leaveTypeId,
        DateTime startAt,
        DateTime endAt,
        string reason,
        LeaveStatus status,
        int currentLevel,
        params (int Level, string Role, ApprovalDecision Decision, int? ApproverUserId, string? Comment, DateTime? DecidedAt)[] steps)
    {
        var request = new LeaveRequest
        {
            StudentId = student.Id,
            LeaveTypeId = leaveTypeId,
            StartAt = startAt,
            EndAt = endAt,
            TotalHours = LeaveCalculatorRules.Hours(startAt, endAt),
            Reason = reason,
            Status = status,
            CurrentLevel = currentLevel,
            CreatedAt = startAt.AddDays(-3),
            UpdatedAt = startAt.AddDays(-3),
            Steps = steps.Select(s => new LeaveRequestStep
            {
                Level = s.Level,
                ApproverRole = s.Role,
                Decision = s.Decision,
                ApproverUserId = s.ApproverUserId,
                Comment = s.Comment,
                DecidedAt = s.DecidedAt
            }).ToList()
        };

        db.LeaveRequests.Add(request);
        db.SaveChanges();   // 立刻存檔以取得 Id（後續通知連結要用）
        return request.Id;
    }
}

/// <summary>
/// 與 <c>Services/LeaveCalculator.cs</c> 相同的上課時段規則（08:00~12:00、13:00~17:00），
/// 用來讓示範假單的 TotalHours 與系統實際計算結果一致。
/// </summary>
internal static class LeaveCalculatorRules
{
    public static decimal Hours(DateTime startAt, DateTime endAt)
    {
        decimal total = 0m;
        for (var day = startAt.Date; day <= endAt.Date; day = day.AddDays(1))
        {
            total += Overlap(startAt, endAt, day.AddHours(8), day.AddHours(12));
            total += Overlap(startAt, endAt, day.AddHours(13), day.AddHours(17));
        }
        return total;
    }

    private static decimal Overlap(DateTime aStart, DateTime aEnd, DateTime bStart, DateTime bEnd)
    {
        var start = aStart > bStart ? aStart : bStart;
        var end = aEnd < bEnd ? aEnd : bEnd;
        return end <= start ? 0m : (decimal)(end - start).TotalHours;
    }
}

/// <summary>截圖流程需要知道的識別碼。</summary>
public sealed class DemoDataIds
{
    public int CohortId { get; set; }

    /// <summary>停在導師關的待簽核假單 Id（用於 /Approvals/Details/{id} 截圖）。</summary>
    public int PendingTutorRequestId { get; set; }
}
