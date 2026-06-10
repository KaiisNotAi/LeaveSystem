using System.ComponentModel.DataAnnotations;
using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 簽核步驟實體（一張請假單的一關）。
/// 一張 LeaveRequest 會有 1~3 筆 Step（依 ApprovalRule 而定）。
/// 申請送出時所有步驟會一次建立，初始狀態為 Pending；
/// 簽核流程引擎逐關喚醒，當前關卡簽核後才換下一關。
/// </summary>
public class LeaveRequestStep
{
    public int Id { get; set; }

    /// <summary>
    /// 所屬請假單 Id。
    /// </summary>
    [Required]
    public int LeaveRequestId { get; set; }

    /// <summary>
    /// 此關的層級（1-based）。1=第一關、2=第二關…
    /// 應與 LeaveRequest.CurrentLevel 比對判斷是否輪到此關。
    /// </summary>
    [Required]
    [Range(1, 10)]
    public int Level { get; set; }

    /// <summary>
    /// 此關需要的角色（例如「Tutor」「SectionChief」）。
    /// 用字串而非 enum 是為了與 ApprovalRule.RequiredRoles JSON 一致。
    /// </summary>
    [Required]
    [StringLength(50)]
    public string ApproverRole { get; set; } = string.Empty;

    /// <summary>
    /// 實際簽核者的 UserId。簽核完成後才填入。
    /// 在還沒被簽核前為 null（系統可從班期 + 角色推算「應簽核人」，但不在此固定）。
    /// </summary>
    public int? ApproverUserId { get; set; }

    /// <summary>
    /// 簽核決定。預設 Pending。
    /// </summary>
    public ApprovalDecision Decision { get; set; } = ApprovalDecision.Pending;

    /// <summary>
    /// 簽核意見/駁回原因。Decision=Rejected 時必填。
    /// </summary>
    [StringLength(500)]
    public string? Comment { get; set; }

    /// <summary>
    /// 簽核時間。Pending 時為 null。
    /// </summary>
    public DateTime? DecidedAt { get; set; }

    // ─── 導覽屬性 ───

    /// <summary>
    /// 所屬請假單。
    /// </summary>
    public LeaveRequest LeaveRequest { get; set; } = null!;

    /// <summary>
    /// 簽核者帳號（簽核完才有值）。
    /// </summary>
    public UserAccount? Approver { get; set; }
}
