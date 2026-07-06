using LeaveSystem.Data;
using LeaveSystem.Models.Entities;

// 「UserRole」在兩個 namespace 都有(Entities 是橋接表、Enums 是列舉),
// 直接 using LeaveSystem.Models.Enums 會讓 `new UserRole { ... }` 模糊化。
// 這裡對需要用到的 enum 做 alias，只保留 Entities namespace 直接可見（體例對齊 ReportTestScenario）。
using UserRoleEnum = LeaveSystem.Models.Enums.UserRole;

namespace LeaveSystem.Tests.Helpers;

/// <summary>
/// Phase 6 曠課紀錄測試專用「多筆多學員」資料情境。
///
/// 情境內容:
/// <code>
///   班期 A (Id=1, "A班-2026春") — 學員 A (Id=100, "小明")
///   班期 B (Id=2, "B班-2026春") — 學員 B (Id=101, "小華")
///   行政人員 (Id=200, "行政小姐")
///   路人甲 (Id=999, 未指派任何角色)                ← 用來測「Create 給非學員 UserId 應失敗」
///
///   ─── 已存在 4 筆曠課紀錄（均由 Staff 200 登錄）───
///     A1 學員 A  2026-06-08 08:00   2h  "遲到過早"     ← 6 月
///     A2 學員 A  2026-06-15 13:00   4h  "下午未到"     ← 6 月
///     A3 學員 A  2026-07-10 08:00   1h  null            ← 7 月
///     B1 學員 B  2026-06-20 08:00   8h  "整天未到"     ← 6 月
/// </code>
///
/// 統計快速查表（供測試斷言用）：
///   ‧ 學員 A 終身曠課 = 2 + 4 + 1 = 7h（3 筆）
///   ‧ 學員 B 終身曠課 = 8h（1 筆）
///   ‧ 全部總時數 = 15h（4 筆）
///   ‧ 6 月篩選 = A1 + A2 + B1 = 14h（3 筆）
///   ‧ 7 月篩選 = A3 = 1h（1 筆）
///   ‧ CohortId=A 篩選 = A1 + A2 + A3 = 7h（3 筆）
///   ‧ CohortId=B 篩選 = B1 = 8h（1 筆）
///
/// 為什麼固定 Id? 讓測試斷言可直接用常數（<c>AbsenceRecordTestScenario.StudentUserIdA</c>），
/// 比每次 <c>db.Users.First(...)</c> 讀更直觀,也讓失敗訊息更好懂。
///
/// 用法:
/// <code>
///   using var scenario = await AbsenceRecordTestScenario.CreateAsync();
///   var sut = new AbsenceRecordService(scenario.Db);
///   ...
/// </code>
/// </summary>
public sealed class AbsenceRecordTestScenario : IDisposable
{
    // ─── 常用固定 Id ───
    public const int CohortIdA = 1;
    public const int CohortIdB = 2;
    public const int StudentUserIdA = 100;
    public const int StudentUserIdB = 101;
    public const int StaffUserId = 200;
    public const int OutsiderUserId = 999;

    /// <summary>
    /// 已 seed 完成的 DbContext。測試結束時記得 dispose（<c>using var</c> 會自動做）。
    /// </summary>
    public AppDbContext Db { get; }

    /// <summary>A1 紀錄的 Id（InMemory 產生）。</summary>
    public int RecordAId1 { get; private init; }

    /// <summary>A2 紀錄的 Id。</summary>
    public int RecordAId2 { get; private init; }

    /// <summary>A3 紀錄的 Id。</summary>
    public int RecordAId3 { get; private init; }

    /// <summary>B1 紀錄的 Id。</summary>
    public int RecordBId1 { get; private init; }

    private AbsenceRecordTestScenario(AppDbContext db) => Db = db;

    /// <summary>
    /// 建立乾淨 InMemory DbContext 並填入 Phase 6 標準情境資料。
    /// </summary>
    public static async Task<AbsenceRecordTestScenario> CreateAsync()
    {
        var db = InMemoryDbContextFactory.CreateEmpty();

        // ─── Roles（Id 與 Enums.UserRole 數值對齊，與 SeedData 一致）───
        db.Roles.AddRange(
            new Role { Id = (int)UserRoleEnum.Admin,   Name = "Admin",   DisplayName = "系統管理員" },
            new Role { Id = (int)UserRoleEnum.Staff,   Name = "Staff",   DisplayName = "行政人員" },
            new Role { Id = (int)UserRoleEnum.Student, Name = "Student", DisplayName = "學員" }
        );

        // ─── 兩班期（不指派簽核人，曠課測試用不到）───
        db.Cohorts.AddRange(
            new Cohort
            {
                Id = CohortIdA,
                Name = "A班-2026春",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalHours = 900,
                LeaveLimitPercent = 10
            },
            new Cohort
            {
                Id = CohortIdB,
                Name = "B班-2026春",
                StartDate = new DateTime(2026, 1, 1),
                EndDate = new DateTime(2026, 12, 31),
                TotalHours = 800,
                LeaveLimitPercent = 10
            }
        );

        // ─── 使用者：兩位學員 + 一位行政 + 一位路人（無 Student 角色）───
        db.UserAccounts.AddRange(
            new UserAccount { Id = StudentUserIdA, Username = "studentA", PasswordHash = "x", DisplayName = "小明",   CohortId = CohortIdA },
            new UserAccount { Id = StudentUserIdB, Username = "studentB", PasswordHash = "x", DisplayName = "小華",   CohortId = CohortIdB },
            new UserAccount { Id = StaffUserId,    Username = "staff01",  PasswordHash = "x", DisplayName = "行政小姐" },
            new UserAccount { Id = OutsiderUserId, Username = "outsider", PasswordHash = "x", DisplayName = "路人甲" }
        );

        db.UserRoles.AddRange(
            new UserRole { UserId = StudentUserIdA, RoleId = (int)UserRoleEnum.Student },
            new UserRole { UserId = StudentUserIdB, RoleId = (int)UserRoleEnum.Student },
            new UserRole { UserId = StaffUserId,    RoleId = (int)UserRoleEnum.Staff }
            // 路人 OutsiderUserId 刻意「不」指派角色，用來測 Service 的角色檢查
        );

        // ─── 4 筆已存在曠課紀錄 ───
        var createdAt = new DateTime(2026, 8, 1, 10, 0, 0, DateTimeKind.Utc);
        var a1 = new AbsenceRecord
        {
            StudentId = StudentUserIdA,
            OccurredAt = new DateTime(2026, 6, 8, 8, 0, 0),
            Hours = 2m,
            Note = "遲到過早",
            CreatedByUserId = StaffUserId,
            CreatedAt = createdAt
        };
        var a2 = new AbsenceRecord
        {
            StudentId = StudentUserIdA,
            OccurredAt = new DateTime(2026, 6, 15, 13, 0, 0),
            Hours = 4m,
            Note = "下午未到",
            CreatedByUserId = StaffUserId,
            CreatedAt = createdAt
        };
        var a3 = new AbsenceRecord
        {
            StudentId = StudentUserIdA,
            OccurredAt = new DateTime(2026, 7, 10, 8, 0, 0),
            Hours = 1m,
            Note = null,
            CreatedByUserId = StaffUserId,
            CreatedAt = createdAt
        };
        var b1 = new AbsenceRecord
        {
            StudentId = StudentUserIdB,
            OccurredAt = new DateTime(2026, 6, 20, 8, 0, 0),
            Hours = 8m,
            Note = "整天未到",
            CreatedByUserId = StaffUserId,
            CreatedAt = createdAt
        };
        db.AbsenceRecords.AddRange(a1, a2, a3, b1);

        await db.SaveChangesAsync();

        // 讀回 InMemory 產生的 Id
        return new AbsenceRecordTestScenario(db)
        {
            RecordAId1 = a1.Id,
            RecordAId2 = a2.Id,
            RecordAId3 = a3.Id,
            RecordBId1 = b1.Id
        };
    }

    public void Dispose() => Db.Dispose();
}
