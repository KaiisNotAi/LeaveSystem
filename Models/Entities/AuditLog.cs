using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 操作稽核日誌。
/// 記錄重要操作（登入、建立帳號、簽核、修改規則等），方便日後追查。
/// </summary>
public class AuditLog
{
    public int Id { get; set; }

    /// <summary>
    /// 操作者 UserId。系統自動操作（如排程）時可為 null。
    /// </summary>
    public int? UserId { get; set; }

    /// <summary>
    /// 動作（如「Login」「CreateUser」「ApproveLeave」「RejectLeave」）。
    /// </summary>
    [Required]
    [StringLength(50)]
    public string Action { get; set; } = string.Empty;

    /// <summary>
    /// 被操作的實體類型（如「UserAccount」「LeaveRequest」）。
    /// </summary>
    [StringLength(50)]
    public string? EntityType { get; set; }

    /// <summary>
    /// 被操作的實體 Id。
    /// </summary>
    public int? EntityId { get; set; }

    /// <summary>
    /// 詳細內容（JSON 字串或描述文字）。
    /// </summary>
    [StringLength(1000)]
    public string? Detail { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── 導覽屬性 ───

    /// <summary>
    /// 操作者。
    /// </summary>
    public UserAccount? User { get; set; }
}
