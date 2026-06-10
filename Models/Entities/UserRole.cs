namespace LeaveSystem.Models.Entities;

/// <summary>
/// 使用者-角色 多對多橋接表。
/// EF Core 從 .NET 5 起支援自動產生橋接表，但這裡顯式建立，
/// 是為了讓初學者能直接從資料庫看到關聯紀錄，理解多對多概念。
///
/// 注意：類別名稱「UserRole」與 Enums 命名空間裡的「UserRole」相同，
/// 但它們在不同 namespace（Entities vs Enums），互不干擾。
/// </summary>
public class UserRole
{
    /// <summary>
    /// 使用者 Id（複合主鍵之一）。
    /// </summary>
    public int UserId { get; set; }

    /// <summary>
    /// 角色 Id（複合主鍵之一）。
    /// </summary>
    public int RoleId { get; set; }

    // ─── 導覽屬性 ───

    /// <summary>
    /// 對應的使用者帳號。
    /// </summary>
    public UserAccount User { get; set; } = null!;

    /// <summary>
    /// 對應的角色。
    /// </summary>
    public Role Role { get; set; } = null!;
}
