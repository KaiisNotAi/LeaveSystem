using LeaveSystem.Models.Entities;
using UserRoleEnum = LeaveSystem.Models.Enums.UserRole;
using UserRoleEntity = LeaveSystem.Models.Entities.UserRole;

namespace LeaveSystem.Data;

/// <summary>
/// 資料庫種子初始化。
/// 在應用程式啟動時呼叫一次（Program.cs），確保系統有最低限度的可用資料：
///   - 6 個角色字典
///   - 1 個預設 Admin 帳號（密碼 Admin@123，首次登入請改）
///   - 4 種預設假別
///   - 3 條預設簽核規則
///
/// 注意：每次呼叫都會檢查資料是否已存在，已存在就跳過，因此可重複執行。
/// </summary>
public static class SeedData
{
    /// <summary>
    /// 預設管理員的帳號與密碼（僅用於初次部署，正式上線後請立即修改）。
    /// </summary>
    public const string DefaultAdminUsername = "Admin";
    public const string DefaultAdminPassword = "Admin@123";

    /// <summary>
    /// 執行所有種子資料初始化。
    /// </summary>
    public static void Initialize(AppDbContext db)
    {
        // 步驟 1：種子角色字典
        SeedRoles(db);

        // 步驟 2：種子預設管理員帳號（並掛上 Admin 角色）
        SeedAdminUser(db);

        // 步驟 3：種子預設假別
        SeedLeaveTypes(db);

        // 步驟 4：種子預設簽核規則
        SeedApprovalRules(db);

        db.SaveChanges();
    }

    private static void SeedRoles(AppDbContext db)
    {
        if (db.Roles.Any()) return;

        db.Roles.AddRange(
            new Role { Id = (int)UserRoleEnum.Admin, Name = nameof(UserRoleEnum.Admin), DisplayName = "系統管理員" },
            new Role { Id = (int)UserRoleEnum.Staff, Name = nameof(UserRoleEnum.Staff), DisplayName = "行政人員" },
            new Role { Id = (int)UserRoleEnum.Tutor, Name = nameof(UserRoleEnum.Tutor), DisplayName = "導師" },
            new Role { Id = (int)UserRoleEnum.SectionChief, Name = nameof(UserRoleEnum.SectionChief), DisplayName = "科長" },
            new Role { Id = (int)UserRoleEnum.BranchDirector, Name = nameof(UserRoleEnum.BranchDirector), DisplayName = "分署長" },
            new Role { Id = (int)UserRoleEnum.Student, Name = nameof(UserRoleEnum.Student), DisplayName = "學員" }
        );

        db.SaveChanges();
    }

    private static void SeedAdminUser(AppDbContext db)
    {
        if (db.UserAccounts.Any(u => u.Username == DefaultAdminUsername)) return;

        var admin = new UserAccount
        {
            Username = DefaultAdminUsername,
            DisplayName = "系統管理員",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(DefaultAdminPassword),
            IsActive = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        db.UserAccounts.Add(admin);
        db.SaveChanges();

        db.UserRoles.Add(new UserRoleEntity
        {
            UserId = admin.Id,
            RoleId = (int)UserRoleEnum.Admin
        });
        db.SaveChanges();
    }

    private static void SeedLeaveTypes(AppDbContext db)
    {
        if (db.LeaveTypes.Any()) return;

        db.LeaveTypes.AddRange(
            new LeaveType { Name = "病假", Code = "SICK", IsActive = true, SortOrder = 1, Description = "因病無法到課" },
            new LeaveType { Name = "事假", Code = "PERSONAL", IsActive = true, SortOrder = 2, Description = "個人事務" },
            new LeaveType { Name = "喪假", Code = "BEREAVEMENT", IsActive = true, SortOrder = 3, Description = "親屬喪事" },
            new LeaveType { Name = "公假", Code = "OFFICIAL", IsActive = true, SortOrder = 4, Description = "因公務外出" }
        );

        db.SaveChanges();
    }

    private static void SeedApprovalRules(AppDbContext db)
    {
        if (db.ApprovalRules.Any()) return;

        db.ApprovalRules.AddRange(
            new ApprovalRule
            {
                MinHours = 0m,
                MaxHours = 8m,
                RequiredRoles = "[\"Tutor\"]",
                Description = "8 小時內由導師決行"
            },
            new ApprovalRule
            {
                MinHours = 8.01m,
                MaxHours = 24m,
                RequiredRoles = "[\"Tutor\",\"SectionChief\"]",
                Description = "8 小時以上至 24 小時，導師→科長"
            },
            new ApprovalRule
            {
                MinHours = 24.01m,
                MaxHours = null,
                RequiredRoles = "[\"Tutor\",\"SectionChief\",\"BranchDirector\"]",
                Description = "24 小時以上，導師→科長→分署長"
            }
        );

        db.SaveChanges();
    }
}
