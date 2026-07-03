using LeaveSystem.Models.ViewModels;

namespace LeaveSystem.Services;

/// <summary>
/// 簽核引擎服務介面。
/// 集中處理「待辦查詢、歷史查詢、詳情查詢、核准、駁回」業務規則。
/// </summary>
public interface IApprovalService
{
    Task<IReadOnlyList<PendingApprovalItem>> GetPendingForUserAsync(int currentUserId);

    Task<IReadOnlyList<ApprovalHistoryItem>> GetHistoryForUserAsync(int currentUserId);

    Task<ApprovalDetailViewModel?> GetDetailForUserAsync(int leaveRequestId, int currentUserId);

    Task<ApprovalActionResult> ApproveAsync(int stepId, int currentUserId, string? comment);

    Task<ApprovalActionResult> RejectAsync(int stepId, int currentUserId, string comment);
}

/// <summary>
/// 簽核動作結果。
/// Success=false 時，ErrorMessage 會帶可顯示給使用者的錯誤訊息。
/// </summary>
public record ApprovalActionResult(bool Success, string? ErrorMessage, ApprovalOutcome Outcome);

/// <summary>
/// 簽核動作結果分類（讓 Controller 可決定顯示訊息）。
/// </summary>
public enum ApprovalOutcome
{
    None = 0,
    AdvancedToNextLevel = 1,
    FullyApproved = 2,
    Rejected = 3
}
