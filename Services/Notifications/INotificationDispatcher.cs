namespace LeaveSystem.Services.Notifications;

/// <summary>
/// 通知派送介面。封裝「站內通知 + Email」雙通道，供簽核流程呼叫。
/// 任一通道失敗都不應擲例外，不影響業務流程。
/// </summary>
public interface INotificationDispatcher
{
    /// <summary>學員送單後，通知第一關簽核人。</summary>
    Task NotifySubmittedAsync(int leaveRequestId, CancellationToken cancellationToken = default);

    /// <summary>某關通過後，通知下一關簽核人（依 LeaveRequest.CurrentLevel 判斷）。</summary>
    Task NotifyApprovedNextAsync(int leaveRequestId, CancellationToken cancellationToken = default);

    /// <summary>全流程通過，通知學員。</summary>
    Task NotifyFullyApprovedAsync(int leaveRequestId, CancellationToken cancellationToken = default);

    /// <summary>任一關駁回，通知學員（含駁回原因）。</summary>
    Task NotifyRejectedAsync(int leaveRequestId, string rejectComment, CancellationToken cancellationToken = default);
}
