using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Services.Email;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace LeaveSystem.Services.Notifications;

/// <inheritdoc />
public class NotificationDispatcher : INotificationDispatcher
{
    private const string RoleTutor = "Tutor";
    private const string RoleSectionChief = "SectionChief";
    private const string RoleBranchDirector = "BranchDirector";

    private readonly AppDbContext _db;
    private readonly INotificationService _notifications;
    private readonly IEmailSender _emails;
    private readonly ILogger<NotificationDispatcher> _logger;

    public NotificationDispatcher(
        AppDbContext db,
        INotificationService notifications,
        IEmailSender emails,
        ILogger<NotificationDispatcher> logger)
    {
        _db = db;
        _notifications = notifications;
        _emails = emails;
        _logger = logger;
    }

    public Task NotifySubmittedAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        => NotifyApproverAtCurrentLevelAsync(leaveRequestId, isFirstSubmission: true, cancellationToken);

    public Task NotifyApprovedNextAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        => NotifyApproverAtCurrentLevelAsync(leaveRequestId, isFirstSubmission: false, cancellationToken);

    public async Task NotifyFullyApprovedAsync(int leaveRequestId, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = await LoadRequestAsync(leaveRequestId, cancellationToken);
            if (req is null) return;

            var url = BuildRequestUrl(leaveRequestId);
            var title = "您的請假申請已核准";
            var message = $"您於 {req.CreatedAt:yyyy/MM/dd} 送出的請假申請（{req.LeaveType?.Name} {req.TotalHours:0.##}h）已完成所有簽核，狀態：核准。";
            await DispatchAsync(req.Student, title, message, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyFullyApprovedAsync 失敗（RequestId={Id}）", leaveRequestId);
        }
    }

    public async Task NotifyRejectedAsync(int leaveRequestId, string rejectComment, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = await LoadRequestAsync(leaveRequestId, cancellationToken);
            if (req is null) return;

            var url = BuildRequestUrl(leaveRequestId);
            var title = "您的請假申請已駁回";
            var message = $"您於 {req.CreatedAt:yyyy/MM/dd} 送出的請假申請已被駁回。駁回原因：{rejectComment}";
            await DispatchAsync(req.Student, title, message, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyRejectedAsync 失敗（RequestId={Id}）", leaveRequestId);
        }
    }

    public async Task NotifyCancelledAsync(int leaveRequestId, CancellationToken cancellationToken = default)
    {
        try
        {
            var req = await LoadRequestAsync(leaveRequestId, cancellationToken);
            if (req is null || req.Student.Cohort is null) return;

            var currentStep = await _db.LeaveRequestSteps
                .Where(s => s.LeaveRequestId == leaveRequestId && s.Level == req.CurrentLevel)
                .FirstOrDefaultAsync(cancellationToken);
            if (currentStep is null) return;

            var approverId = ResolveApproverUserId(req.Student.Cohort, currentStep.ApproverRole);
            if (approverId is null) return;

            var approver = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == approverId.Value, cancellationToken);
            if (approver is null) return;

            var url = BuildApprovalUrl(leaveRequestId);
            var title = "學員已取消請假申請";
            var message = $"學員 {req.Student.DisplayName} 已取消先前送出的請假申請（{req.LeaveType?.Name} {req.TotalHours:0.##}h），本關可略過。";
            await DispatchAsync(approver, title, message, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NotifyCancelledAsync 失敗（RequestId={Id}）", leaveRequestId);
        }
    }

    private async Task NotifyApproverAtCurrentLevelAsync(int leaveRequestId, bool isFirstSubmission, CancellationToken cancellationToken)
    {
        try
        {
            var req = await LoadRequestAsync(leaveRequestId, cancellationToken);
            if (req is null || req.Student.Cohort is null) return;

            var currentStep = await _db.LeaveRequestSteps
                .Where(s => s.LeaveRequestId == leaveRequestId && s.Level == req.CurrentLevel)
                .FirstOrDefaultAsync(cancellationToken);
            if (currentStep is null) return;

            var approverId = ResolveApproverUserId(req.Student.Cohort, currentStep.ApproverRole);
            if (approverId is null) return;

            var approver = await _db.UserAccounts.FirstOrDefaultAsync(u => u.Id == approverId.Value, cancellationToken);
            if (approver is null) return;

            var url = BuildApprovalUrl(leaveRequestId);
            var title = isFirstSubmission
                ? "您有新的請假申請待簽核"
                : "有請假申請進入您這一關待簽核";
            var message = $"學員 {req.Student.DisplayName} 送出的請假申請（{req.LeaveType?.Name} {req.TotalHours:0.##}h）需要您簽核。";
            await DispatchAsync(approver, title, message, url, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "通知簽核人失敗（RequestId={Id}）", leaveRequestId);
        }
    }

    private async Task DispatchAsync(UserAccount recipient, string title, string message, string url, CancellationToken cancellationToken)
    {
        try
        {
            await _notifications.CreateAsync(recipient.Id, title, message, url);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "站內通知寫入失敗（UserId={UserId}）", recipient.Id);
        }

        if (!string.IsNullOrWhiteSpace(recipient.Email))
        {
            try
            {
                await _emails.SendAsync(recipient.Email!, title, message, cancellationToken);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Email 派送失敗（UserId={UserId}, Email={Email}）", recipient.Id, recipient.Email);
            }
        }
    }

    private Task<LeaveRequest?> LoadRequestAsync(int leaveRequestId, CancellationToken cancellationToken)
    {
        return _db.LeaveRequests
            .Include(r => r.Student).ThenInclude(u => u.Cohort)
            .Include(r => r.LeaveType)
            .FirstOrDefaultAsync(r => r.Id == leaveRequestId, cancellationToken);
    }

    private static int? ResolveApproverUserId(Cohort cohort, string role) => role switch
    {
        RoleTutor => cohort.TutorUserId,
        RoleSectionChief => cohort.SectionChiefUserId,
        RoleBranchDirector => cohort.BranchDirectorUserId,
        _ => null
    };

    private static string BuildRequestUrl(int leaveRequestId) => $"/LeaveRequests/Index";
    private static string BuildApprovalUrl(int leaveRequestId) => $"/Approvals/Detail/{leaveRequestId}";
}

/// <summary>
/// 不做任何事的空實作。用於測試或關閉通知功能時的退路。
/// </summary>
public class NullNotificationDispatcher : INotificationDispatcher
{
    public Task NotifySubmittedAsync(int leaveRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyApprovedNextAsync(int leaveRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyFullyApprovedAsync(int leaveRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyRejectedAsync(int leaveRequestId, string rejectComment, CancellationToken cancellationToken = default) => Task.CompletedTask;
    public Task NotifyCancelledAsync(int leaveRequestId, CancellationToken cancellationToken = default) => Task.CompletedTask;
}
