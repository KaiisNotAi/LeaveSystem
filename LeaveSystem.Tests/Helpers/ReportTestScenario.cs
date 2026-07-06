using LeaveSystem.Data;
using LeaveSystem.Models.Entities;

// 「UserRole」在兩個 namespace 都有(Entities 是橋接表、Enums 是列舉)，
// 直接 using LeaveSystem.Models.Enums 會讓 `new UserRole { ... }` 模糊化。
// 這裡改為對需要用到的 enum 各自 alias，只保留 Entities namespace 直接可見。
using UserRoleEnum = LeaveSystem.Models.Enums.UserRole;
using LeaveStatus = LeaveSystem.Models.Enums.LeaveStatus;
using ApprovalDecision = LeaveSystem.Models.Enums.ApprovalDecision;

namespace LeaveSystem.Tests.Helpers;

/// <summary>
/// Phase 5 紀錄查詢與時數統計測試專用「多筆多狀態」資料情境。
///
/// 情境內容:
/// <code>
///   班期 A (Id=1, "A班-2026春", 900h, 10%) — 學員 A (Id=100, "小明")
///   班期 B (Id=2, "B班-2026春", 800h, 10%) — 學員 B (Id=101, "小華")
///
///   3 個假別: 事假(1), 病假(2), 公假(3)
///
///   ─── 學員 A 的 6 張請假單 ───
///     RA1 2026-06-08 08~12   事假   4h  Approved
///     RA2 2026-06-15 08~17   病假   8h  Approved
///     RA3 2026-07-01 08~12   事假   4h  Approved
///     RA4 2026-08-10 08~17   公假   8h  Pending   (Summary/Admin 不算)
///     RA5 2026-08-20 08~12   事假   4h  Rejected  (Step Comment="資料不足")
///     RA6 2026-06-28 → 07-02 事假  20h  Approved  (跨月，整筆算 6 月)
///
///   ─── 學員 B 的 2 張請假單（驗證不會撈到別人資料）───
///     RB1 2026-06-10 08~17   病假   8h  Approved
///     RB2 2026-07-15 08~12   事假   4h  Approved
/// </code>
///
/// 為什麼固定 Id? 讓測試斷言可直接用常數(<c>ReportTestScenario.StudentUserIdA</c>)，
/// 比每次 <c>db.Users.First(...)</c> 讀更直觀，也讓失敗訊息更好懂。
///
/// 用法:
/// <code>
///   using var scenario = await ReportTestScenario.CreateAsync();
///   var sut = new LeaveReportService(scenario.Db);
///   ...
/// </code>
/// </summary>
public sealed class ReportTestScenario : IDisposable
{
    // ─── 常用固定 Id ───
    public const int CohortIdA = 1;
    public const int CohortIdB = 2;
    public const int TutorUserIdA = 10;
    public const int TutorUserIdB = 11;
    public const int StudentUserIdA = 100;
    public const int StudentUserIdB = 101;

    // 假別 Id
    public const int LeaveTypeIdPersonal = 1;   // 事假
    public const int LeaveTypeIdSick = 2;       // 病假
    public const int LeaveTypeIdOfficial = 3;   // 公假

    /// <summary>
    /// 已 seed 完成的 DbContext。測試結束時記得 dispose(<c>using var</c> 會自動做)。
    /// </summary>
    public AppDbContext Db { get; }

    private ReportTestScenario(AppDbContext db) => Db = db;

    /// <summary>
    /// 建立乾淨 InMemory DbContext 並填入 Phase 5 標準情境資料。
    /// </summary>
    public static async Task<ReportTestScenario> CreateAsync()
    {
        var db = InMemoryDbContextFactory.CreateEmpty();

        // ─── Roles（Id 與 Enums.UserRole 數值對齊，與 SeedData 一致）───
        db.Roles.AddRange(
            new Role { Id = (int)UserRoleEnum.Admin,          Name = "Admin",          DisplayName = "系統管理員" },
            new Role { Id = (int)UserRoleEnum.Staff,          Name = "Staff",          DisplayName = "行政人員" },
            new Role { Id = (int)UserRoleEnum.Tutor,          Name = "Tutor",          DisplayName = "導師" },
            new Role { Id = (int)UserRoleEnum.SectionChief,   Name = "SectionChief",   DisplayName = "科長" },
            new Role { Id = (int)UserRoleEnum.BranchDirector, Name = "BranchDirector", DisplayName = "分署長" },
            new Role { Id = (int)UserRoleEnum.Student,        Name = "Student",        DisplayName = "學員" }
        );

        // ─── 兩位導師 + 兩位學員 ───
        db.UserAccounts.AddRange(
            new UserAccount { Id = TutorUserIdA,   Username = "tutorA",   PasswordHash = "x", DisplayName = "A班導師" },
            new UserAccount { Id = TutorUserIdB,   Username = "tutorB",   PasswordHash = "x", DisplayName = "B班導師" },
            new UserAccount { Id = StudentUserIdA, Username = "studentA", PasswordHash = "x", DisplayName = "小明", CohortId = CohortIdA },
            new UserAccount { Id = StudentUserIdB, Username = "studentB", PasswordHash = "x", DisplayName = "小華", CohortId = CohortIdB }
        );
        db.UserRoles.AddRange(
            new UserRole { UserId = TutorUserIdA,   RoleId = (int)UserRoleEnum.Tutor },
            new UserRole { UserId = TutorUserIdB,   RoleId = (int)UserRoleEnum.Tutor },
            new UserRole { UserId = StudentUserIdA, RoleId = (int)UserRoleEnum.Student },
            new UserRole { UserId = StudentUserIdB, RoleId = (int)UserRoleEnum.Student }
        );

        // ─── 兩個班期 ───
        db.Cohorts.AddRange(
            new Cohort
            {
                Id = CohortIdA,
                Name = "A班-2026春",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalHours = 900,
                LeaveLimitPercent = 10,        // 上限 = 90h
                TutorUserId = TutorUserIdA
            },
            new Cohort
            {
                Id = CohortIdB,
                Name = "B班-2026春",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalHours = 800,
                LeaveLimitPercent = 10,        // 上限 = 80h
                TutorUserId = TutorUserIdB
            }
        );

        // ─── 三種假別 ───
        db.LeaveTypes.AddRange(
            new LeaveType { Id = LeaveTypeIdPersonal, Name = "事假", Code = "PERSONAL", IsActive = true, SortOrder = 1 },
            new LeaveType { Id = LeaveTypeIdSick,     Name = "病假", Code = "SICK",     IsActive = true, SortOrder = 2 },
            new LeaveType { Id = LeaveTypeIdOfficial, Name = "公假", Code = "OFFICIAL", IsActive = true, SortOrder = 3 }
        );

        // ─── 學員 A 的 6 張單 ───
        AddRequest(db, StudentUserIdA, LeaveTypeIdPersonal,
            new DateTime(2026, 6, 8, 8, 0, 0), new DateTime(2026, 6, 8, 12, 0, 0),
            4m, LeaveStatus.Approved, "6月事假");

        AddRequest(db, StudentUserIdA, LeaveTypeIdSick,
            new DateTime(2026, 6, 15, 8, 0, 0), new DateTime(2026, 6, 15, 17, 0, 0),
            8m, LeaveStatus.Approved, "6月病假");

        AddRequest(db, StudentUserIdA, LeaveTypeIdPersonal,
            new DateTime(2026, 7, 1, 8, 0, 0), new DateTime(2026, 7, 1, 12, 0, 0),
            4m, LeaveStatus.Approved, "7月事假");

        AddRequest(db, StudentUserIdA, LeaveTypeIdOfficial,
            new DateTime(2026, 8, 10, 8, 0, 0), new DateTime(2026, 8, 10, 17, 0, 0),
            8m, LeaveStatus.Pending, "8月公假(送審中)");

        AddRequest(db, StudentUserIdA, LeaveTypeIdPersonal,
            new DateTime(2026, 8, 20, 8, 0, 0), new DateTime(2026, 8, 20, 12, 0, 0),
            4m, LeaveStatus.Rejected, "8月事假(被駁回)",
            rejectComment: "資料不足");

        AddRequest(db, StudentUserIdA, LeaveTypeIdPersonal,
            new DateTime(2026, 6, 28, 8, 0, 0), new DateTime(2026, 7, 2, 17, 0, 0),
            20m, LeaveStatus.Approved, "跨月事假(整筆算6月)");

        // ─── 學員 B 的 2 張單（用來驗證「不會撈到別人的」）───
        AddRequest(db, StudentUserIdB, LeaveTypeIdSick,
            new DateTime(2026, 6, 10, 8, 0, 0), new DateTime(2026, 6, 10, 17, 0, 0),
            8m, LeaveStatus.Approved, "B同學6月病假");

        AddRequest(db, StudentUserIdB, LeaveTypeIdPersonal,
            new DateTime(2026, 7, 15, 8, 0, 0), new DateTime(2026, 7, 15, 12, 0, 0),
            4m, LeaveStatus.Approved, "B同學7月事假");

        await db.SaveChangesAsync();
        return new ReportTestScenario(db);
    }

    /// <summary>
    /// 建一張請假單。若是 Rejected 且有 rejectComment，會順便建一筆 Rejected 的 Step，
    /// 讓 <c>MyLeaveRequestListItem.RejectReason</c> 有值可測。
    /// </summary>
    private static void AddRequest(
        AppDbContext db,
        int studentId,
        int leaveTypeId,
        DateTime startAt,
        DateTime endAt,
        decimal totalHours,
        LeaveStatus status,
        string reason,
        string? rejectComment = null)
    {
        // CreatedAt 設為送出時間（假設在請假日前 3 天送出）
        var createdAt = startAt.AddDays(-3);

        var request = new LeaveRequest
        {
            StudentId = studentId,
            LeaveTypeId = leaveTypeId,
            StartAt = startAt,
            EndAt = endAt,
            TotalHours = totalHours,
            Reason = reason,
            Status = status,
            CurrentLevel = 1,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        };

        // 只為 Rejected 單建立一筆 Rejected step，讓 RejectReason 有值。
        // Phase 4 邏輯：任一級駁回整張單終止，所以最多一筆 Rejected step。
        if (status == LeaveStatus.Rejected && !string.IsNullOrWhiteSpace(rejectComment))
        {
            request.Steps.Add(new LeaveRequestStep
            {
                Level = 1,
                ApproverRole = "Tutor",
                Decision = ApprovalDecision.Rejected,
                Comment = rejectComment,
                DecidedAt = createdAt.AddDays(1)
            });
        }

        db.LeaveRequests.Add(request);
    }

    public void Dispose() => Db.Dispose();
}
