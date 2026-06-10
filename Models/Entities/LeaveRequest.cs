using System.ComponentModel.DataAnnotations;
using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 請假申請主檔。
/// 一筆紀錄代表一張完整的請假單；它的逐關簽核紀錄存在 LeaveRequestStep 中。
/// </summary>
public class LeaveRequest
{
    public int Id { get; set; }

    /// <summary>
    /// 申請人（學員）UserId。
    /// </summary>
    [Required]
    public int StudentId { get; set; }

    /// <summary>
    /// 假別 Id（對應 LeaveType）。
    /// </summary>
    [Required]
    public int LeaveTypeId { get; set; }

    /// <summary>
    /// 請假開始時間（包含日期 + 上課時段的開始）。
    /// 例：6/9 08:00。
    /// </summary>
    [Required]
    public DateTime StartAt { get; set; }

    /// <summary>
    /// 請假結束時間（包含日期 + 上課時段的結束）。
    /// 例：6/9 17:00。
    /// </summary>
    [Required]
    public DateTime EndAt { get; set; }

    /// <summary>
    /// 請假總時數（最小單位 1 小時）。
    /// 由 LeaveCalculator 服務根據 StartAt/EndAt 自動計算（扣除午休與下班時段）。
    /// </summary>
    [Range(1, 1000)]
    public decimal TotalHours { get; set; }

    /// <summary>
    /// 請假原因說明。
    /// </summary>
    [Required(ErrorMessage = "請假原因為必填")]
    [StringLength(500, ErrorMessage = "請假原因最長 500 字")]
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 整張單目前狀態。預設 Pending（提交即進入簽核中）。
    /// </summary>
    public LeaveStatus Status { get; set; } = LeaveStatus.Pending;

    /// <summary>
    /// 目前停在第幾關（1-based）。Status=Pending 時才有意義。
    /// 例：1=導師審核中、2=科長審核中、3=分署長審核中。
    /// </summary>
    public int CurrentLevel { get; set; } = 1;

    /// <summary>
    /// 建立時間（UTC）。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 最後更新時間（UTC）。每次簽核或撤銷時更新。
    /// </summary>
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    // ─── 導覽屬性 ───

    /// <summary>
    /// 申請人。
    /// </summary>
    public UserAccount Student { get; set; } = null!;

    /// <summary>
    /// 假別。
    /// </summary>
    public LeaveType LeaveType { get; set; } = null!;

    /// <summary>
    /// 此申請的逐關簽核步驟（依 Level 排序）。
    /// </summary>
    public ICollection<LeaveRequestStep> Steps { get; set; } = new List<LeaveRequestStep>();
}
