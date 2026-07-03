using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 待我簽核清單列資料。
/// </summary>
public class PendingApprovalItem
{
    public int LeaveRequestId { get; set; }
    public int StepId { get; set; }
    public int CurrentLevel { get; set; }
    public string StudentDisplayName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal TotalHours { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 我已簽過的歷史清單列資料。
/// </summary>
public class ApprovalHistoryItem
{
    public int LeaveRequestId { get; set; }
    public int StepId { get; set; }
    public int Level { get; set; }
    public string StudentDisplayName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>
/// 詳細頁的單一簽核步驟顯示模型。
/// </summary>
public class ApprovalStepViewModel
{
    public int StepId { get; set; }
    public int Level { get; set; }
    public string ApproverRole { get; set; } = string.Empty;
    public ApprovalDecision Decision { get; set; }
    public int? ApproverUserId { get; set; }
    public string? ApproverDisplayName { get; set; }
    public string? Comment { get; set; }
    public DateTime? DecidedAt { get; set; }
}

/// <summary>
/// 簽核詳細頁模型（主檔 + 步驟 + 是否可動作）。
/// </summary>
public class ApprovalDetailViewModel
{
    public int LeaveRequestId { get; set; }
    public int CurrentLevel { get; set; }
    public LeaveStatus Status { get; set; }
    public string StudentDisplayName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal TotalHours { get; set; }
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 當前使用者是否可對目前關卡執行核准/駁回。
    /// </summary>
    public bool CanActOnCurrentStep { get; set; }

    /// <summary>
    /// 若可動作，這裡會帶目前關卡的 StepId，供表單提交。
    /// </summary>
    public int? CurrentStepId { get; set; }

    public List<ApprovalStepViewModel> Steps { get; set; } = new();
}

/// <summary>
/// 詳細頁提交核准/駁回時的輸入模型。
/// </summary>
public class ApprovalActionInputModel
{
    public int StepId { get; set; }
    public string? Comment { get; set; }
}

/// <summary>
/// 簽核首頁模型（待簽清單 + 已簽歷史）。
/// </summary>
public class ApprovalIndexViewModel
{
    public List<PendingApprovalItem> PendingItems { get; set; } = new();
    public List<ApprovalHistoryItem> HistoryItems { get; set; } = new();
}
