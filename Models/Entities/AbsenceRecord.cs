using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 曠課紀錄實體。
/// 與請假單分開，因為「曠課」是事後由行政人員登記的事實，不需簽核流程。
/// 只有 Admin / Staff 可以新增、修改、刪除；學員只能查閱自己的紀錄。
/// </summary>
public class AbsenceRecord
{
    public int Id { get; set; }

    /// <summary>
    /// 學員 UserId。
    /// </summary>
    [Required]
    public int StudentId { get; set; }

    /// <summary>
    /// 曠課發生時間（含日期與上課時段的開始時間）。
    /// </summary>
    [Required]
    public DateTime OccurredAt { get; set; }

    /// <summary>
    /// 曠課時數（最小單位 1 小時）。
    /// </summary>
    [Range(0.5, 100)]
    public decimal Hours { get; set; }

    /// <summary>
    /// 備註（如「未到課且未請假」「遲到超過 30 分鐘」）。
    /// </summary>
    [StringLength(500)]
    public string? Note { get; set; }

    /// <summary>
    /// 登錄此紀錄的行政人員 UserId（稽核用）。
    /// </summary>
    [Required]
    public int CreatedByUserId { get; set; }

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    // ─── 導覽屬性 ───

    /// <summary>
    /// 學員。
    /// </summary>
    public UserAccount Student { get; set; } = null!;

    /// <summary>
    /// 登錄者。
    /// </summary>
    public UserAccount CreatedByUser { get; set; } = null!;
}
