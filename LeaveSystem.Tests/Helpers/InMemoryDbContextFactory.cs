using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

// 「UserRole」在兩個 namespace 都有(Entities 是橋接表、Enums 是列舉),
// 直接 using LeaveSystem.Models.Enums 會讓 `new UserRole { ... }` 模糊化。
// 這裡改為對需要用到的 enum 各自 alias,只保留 Entities namespace 直接可見。
using UserRoleEnum = LeaveSystem.Models.Enums.UserRole;
using LeaveStatus = LeaveSystem.Models.Enums.LeaveStatus;
using ApprovalDecision = LeaveSystem.Models.Enums.ApprovalDecision;

namespace LeaveSystem.Tests.Helpers;

/// <summary>
/// 建立測試用 AppDbContext 的工廠。
///
/// 使用 EF Core InMemory Provider —— 資料完全放在 process 記憶體裡,
/// 測試跑完就消失,不會污染真的 SQL Server。
///
/// 每次呼叫 <see cref="CreateEmpty"/> 都會產生一個獨立的資料庫(用 Guid 命名),
/// 這樣不同測試同時執行時彼此的資料不會互相干擾。
/// </summary>
public static class InMemoryDbContextFactory
{
    /// <summary>
    /// 建立一個乾淨的 <see cref="AppDbContext"/>(完全沒有資料)。
    /// 測試若需要標準情境資料,請改用 <see cref="ApprovalTestScenario.CreateAsync"/>。
    /// </summary>
    public static AppDbContext CreateEmpty()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new AppDbContext(options);
    }
}

/// <summary>
/// 簽核測試用「標準情境」資料 + DbContext 打包物件。
///
/// 情境內容:
/// <code>
///   班期 (Id=1)
///     ├─ 導師           TutorUserId = 10
///     ├─ 科長           SectionChiefUserId = 20
///     └─ 分署長         BranchDirectorUserId = 30
///
///   學員 (Id=100,屬於這個班期)
///   假別 (Id=1,"事假")
///   路人甲 (Id=999,未擔任任何簽核職)
///
///   一張三關 Pending 請假單 (LeaveRequestId=1,CurrentLevel=1,總時數=30h)
///     ├─ Step Level=1 ApproverRole=Tutor            → Step1TutorId
///     ├─ Step Level=2 ApproverRole=SectionChief     → Step2SectionChiefId
///     └─ Step Level=3 ApproverRole=BranchDirector   → Step3BranchDirectorId
/// </code>
///
/// 為什麼固定 Id?讓測試斷言可直接用常數(<c>ApprovalTestScenario.TutorUserId</c>),
/// 比每次 <c>db.Users.First(...)</c> 讀更直觀,也讓失敗訊息更好懂。
///
/// 用法:
/// <code>
///   using var scenario = await ApprovalTestScenario.CreateAsync();
///   var sut = new ApprovalService(scenario.Db);
///   ...
/// </code>
/// </summary>
public sealed class ApprovalTestScenario : IDisposable
{
    // ─── 常用固定 Id(測試斷言直接用) ───
    public const int CohortId = 1;
    public const int TutorUserId = 10;
    public const int SectionChiefUserId = 20;
    public const int BranchDirectorUserId = 30;
    public const int StudentUserId = 100;
    public const int OutsiderUserId = 999;
    public const int LeaveTypeId = 1;
    public const int LeaveRequestId = 1;

    /// <summary>
    /// 已 seed 完成的 DbContext。測試結束時記得 dispose(<c>using var</c> 會自動做)。
    /// </summary>
    public AppDbContext Db { get; }

    /// <summary>
    /// 第 1 關(導師)的 Step.Id(由 InMemory 產生)。
    /// </summary>
    public int Step1TutorId { get; private init; }

    /// <summary>
    /// 第 2 關(科長)的 Step.Id。
    /// </summary>
    public int Step2SectionChiefId { get; private init; }

    /// <summary>
    /// 第 3 關(分署長)的 Step.Id。
    /// </summary>
    public int Step3BranchDirectorId { get; private init; }

    private ApprovalTestScenario(AppDbContext db) => Db = db;

    /// <summary>
    /// 建立乾淨 InMemory DbContext 並填入標準情境資料,回傳 scenario 物件。
    /// </summary>
    public static async Task<ApprovalTestScenario> CreateAsync()
    {
        var db = InMemoryDbContextFactory.CreateEmpty();

        // ─── Roles(Id 與 Enums.UserRole 數值對齊,與 SeedData 一致) ───
        db.Roles.AddRange(
            new Role { Id = (int)UserRoleEnum.Admin,          Name = "Admin",          DisplayName = "系統管理員" },
            new Role { Id = (int)UserRoleEnum.Staff,          Name = "Staff",          DisplayName = "行政人員" },
            new Role { Id = (int)UserRoleEnum.Tutor,          Name = "Tutor",          DisplayName = "導師" },
            new Role { Id = (int)UserRoleEnum.SectionChief,   Name = "SectionChief",   DisplayName = "科長" },
            new Role { Id = (int)UserRoleEnum.BranchDirector, Name = "BranchDirector", DisplayName = "分署長" },
            new Role { Id = (int)UserRoleEnum.Student,        Name = "Student",        DisplayName = "學員" }
        );

        // ─── 三位簽核人 + 路人甲 ───
        db.UserAccounts.AddRange(
            new UserAccount { Id = TutorUserId,          Username = "tutor01",    PasswordHash = "x", DisplayName = "王導師" },
            new UserAccount { Id = SectionChiefUserId,   Username = "chief01",    PasswordHash = "x", DisplayName = "陳科長" },
            new UserAccount { Id = BranchDirectorUserId, Username = "director01", PasswordHash = "x", DisplayName = "林分署長" },
            new UserAccount { Id = OutsiderUserId,       Username = "outsider",   PasswordHash = "x", DisplayName = "路人甲" }
        );

        db.UserRoles.AddRange(
            new UserRole { UserId = TutorUserId,          RoleId = (int)UserRoleEnum.Tutor },
            new UserRole { UserId = SectionChiefUserId,   RoleId = (int)UserRoleEnum.SectionChief },
            new UserRole { UserId = BranchDirectorUserId, RoleId = (int)UserRoleEnum.BranchDirector }
        );

        // ─── 班期(指派三位簽核人) ───
        db.Cohorts.Add(new Cohort
        {
            Id = CohortId,
            Name = "測試班期 T1",
            StartDate = new DateTime(2026, 1, 1),
            EndDate = new DateTime(2026, 12, 31),
            TotalHours = 900,
            LeaveLimitPercent = 10,
            TutorUserId = TutorUserId,
            SectionChiefUserId = SectionChiefUserId,
            BranchDirectorUserId = BranchDirectorUserId
        });

        // ─── 學員 ───
        db.UserAccounts.Add(new UserAccount
        {
            Id = StudentUserId,
            Username = "student01",
            PasswordHash = "x",
            DisplayName = "測試學員",
            CohortId = CohortId
        });
        db.UserRoles.Add(new UserRole { UserId = StudentUserId, RoleId = (int)UserRoleEnum.Student });

        // ─── 假別 ───
        db.LeaveTypes.Add(new LeaveType
        {
            Id = LeaveTypeId,
            Name = "事假",
            Code = "PERSONAL",
            IsActive = true,
            SortOrder = 1
        });

        // ─── 三關 Pending 請假單 ───
        var createdAt = new DateTime(2026, 6, 1, 10, 0, 0, DateTimeKind.Utc);
        db.LeaveRequests.Add(new LeaveRequest
        {
            Id = LeaveRequestId,
            StudentId = StudentUserId,
            LeaveTypeId = LeaveTypeId,
            StartAt = new DateTime(2026, 6, 8, 8, 0, 0),
            EndAt = new DateTime(2026, 6, 12, 17, 0, 0),
            TotalHours = 30m,
            Reason = "測試用請假單",
            Status = LeaveStatus.Pending,
            CurrentLevel = 1,
            CreatedAt = createdAt,
            UpdatedAt = createdAt
        });

        var step1 = new LeaveRequestStep { LeaveRequestId = LeaveRequestId, Level = 1, ApproverRole = "Tutor",          Decision = ApprovalDecision.Pending };
        var step2 = new LeaveRequestStep { LeaveRequestId = LeaveRequestId, Level = 2, ApproverRole = "SectionChief",   Decision = ApprovalDecision.Pending };
        var step3 = new LeaveRequestStep { LeaveRequestId = LeaveRequestId, Level = 3, ApproverRole = "BranchDirector", Decision = ApprovalDecision.Pending };
        db.LeaveRequestSteps.AddRange(step1, step2, step3);

        await db.SaveChangesAsync();

        // 讀回 InMemory 產生的 Step.Id
        return new ApprovalTestScenario(db)
        {
            Step1TutorId = step1.Id,
            Step2SectionChiefId = step2.Id,
            Step3BranchDirectorId = step3.Id
        };
    }

    public void Dispose() => Db.Dispose();
}
