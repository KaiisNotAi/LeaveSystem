using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 使用者帳號實體。
/// 一筆紀錄代表一個可登入系統的人；他可以同時擁有多個角色（透過 UserRole 多對多）。
/// 學員身分必須有 CohortId（隸屬班期），其他角色（行政/導師/科長/分署長/管理員）則可為 null。
/// </summary>
public class UserAccount
{
    /// <summary>
    /// 主鍵，由資料庫自動產生（IDENTITY）。
    /// </summary>
    public int Id { get; set; }

    /// <summary>
    /// 登入帳號（唯一）。建議使用英數，避免中文以防日後對接其他系統的困擾。
    /// </summary>
    [Required(ErrorMessage = "帳號為必填")]
    [StringLength(50, ErrorMessage = "帳號最長 50 字")]
    public string Username { get; set; } = string.Empty;

    /// <summary>
    /// 密碼雜湊值（使用 BCrypt）。永遠不存明碼。
    /// </summary>
    [Required]
    [StringLength(200)]
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>
    /// 顯示名稱（中文姓名），用於介面顯示與通知署名。
    /// </summary>
    [Required(ErrorMessage = "顯示名稱為必填")]
    [StringLength(50, ErrorMessage = "顯示名稱最長 50 字")]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary>
    /// 電子郵件，用於 Email 通知（Phase 7 才會啟用）。
    /// </summary>
    [EmailAddress(ErrorMessage = "Email 格式不正確")]
    [StringLength(100)]
    public string? Email { get; set; }

    /// <summary>
    /// 所屬班期 Id。僅學員角色需要設值；其他角色可為 null。
    /// </summary>
    public int? CohortId { get; set; }

    /// <summary>
    /// 帳號是否啟用。停用的帳號無法登入，但歷史紀錄仍保留。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 建立時間（UTC）。由系統自動填入。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最後更新時間（UTC）。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── 導覽屬性（Navigation Properties） ───
    // EF Core 透過這些屬性產生 LEFT JOIN 並把關聯資料一起載入。

    /// <summary>
    /// 所屬班期（學員才會有值）。
    /// </summary>
    public Cohort? Cohort { get; set; }

    /// <summary>
    /// 此帳號擁有的所有角色（多對多）。
    /// </summary>
    public ICollection<UserRole> UserRoles { get; set; } = new List<UserRole>();
}
