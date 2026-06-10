using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 角色字典表。
/// 與 Models.Enums.UserRole 對應；資料庫存 6 筆固定資料，由 SeedData 種子化。
/// 為何不直接用 enum 存？因為這樣管理者未來想新增「副導師」等角色時，
/// 不需改 C# 程式碼即可從資料庫加，彈性較大；同時保留 enum 給強型別判斷。
/// </summary>
public class Role
{
    /// <summary>
    /// 主鍵。建議與 Enums.UserRole 的數值一致（1=Admin、2=Staff…）。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 系統內部用的英文名（對應 enum 名稱），用於 [Authorize(Roles="Admin")] 等屬性。
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 顯示用的中文名（如「系統管理員」「導師」），介面顯示用。
    /// </summary>
    [Required]
    [StringLength(50)]
    public string DisplayName { get; set; } = string.Empty;

    // ─── 導覽屬性 ───

    /// <summary>
    /// 擁有此角色的所有使用者（多對多）。
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
