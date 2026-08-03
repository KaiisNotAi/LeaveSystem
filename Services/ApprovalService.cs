using LeaveSystem.Data;
using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services.Notifications;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Services;

/// <summary>
/// 簽核引擎服務實作（Phase 4）。
///
/// 目前先建立 TDD 骨架：
/// - 先讓測試專案可編譯與可執行
/// - 下一步再逐條補齊業務規則，讓測試從 Red 轉 Green
/// </summary>
public class ApprovalService : IApprovalService
{
    private const string RoleTutor = "Tutor";
    private const string RoleSectionChief = "SectionChief";
    private const string RoleBranchDirector = "BranchDirector";

    private const string ErrorStepNotFound = "找不到要簽核的關卡。";
    private const string ErrorRequestNotPending = "此請假單已非簽核中狀態，無法再簽核。";
    private const string ErrorNotCurrentLevel = "目前尚未輪到此關簽核。";
    private const string ErrorAlreadyDecided = "此關卡已簽核完成，不能重複簽核。";
    private const string ErrorRoleMismatch = "你沒有此關卡所需角色，無法簽核。";
    private const string ErrorNotAssignedApprover = "你不是此班期此關卡的指定簽核人。";
    private const string ErrorRejectCommentRequired = "駁回原因為必填。";

    private readonly AppDbContext _db;
    private readonly INotificationDispatcher _notifications;

    public ApprovalService(AppDbContext db, INotificationDispatcher? notifications = null)
    {
        _db = db;
        _notifications = notifications ?? new NullNotificationDispatcher();
    }

    public Task<IReadOnlyList<PendingApprovalItem>> GetPendingForUserAsync(int currentUserId)
    {
        return GetPendingCoreAsync(currentUserId);
    }

    public Task<IReadOnlyList<ApprovalHistoryItem>> GetHistoryForUserAsync(int currentUserId)
    {
        return GetHistoryCoreAsync(currentUserId);
    }

    public Task<ApprovalDetailViewModel?> GetDetailForUserAsync(int leaveRequestId, int currentUserId)
    {
        return GetDetailCoreAsync(leaveRequestId, currentUserId);
    }

    public async Task<ApprovalActionResult> ApproveAsync(int stepId, int currentUserId, string? comment)
    {
        var step = await LoadStepForActionAsync(stepId);
        if (step is null)
        {
            return new ApprovalActionResult(false, ErrorStepNotFound, ApprovalOutcome.None);
        }

        var canAct = await EnsureCanActAsync(step, currentUserId);
        if (!canAct.Success)
        {
            return canAct;
        }

        var now = DateTime.UtcNow;
        var request = step.LeaveRequest;

        step.Decision = ApprovalDecision.Approved;
        step.ApproverUserId = currentUserId;
        step.Comment = string.IsNullOrWhiteSpace(comment) ? null : comment.Trim();
        step.DecidedAt = now;

        var nextStep = await _db.LeaveRequestSteps
            .Where(s => s.LeaveRequestId == request.Id && s.Level > request.CurrentLevel)
            .OrderBy(s => s.Level)
            .FirstOrDefaultAsync();

        ApprovalOutcome outcome;
        if (nextStep is not null)
        {
            request.CurrentLevel = nextStep.Level;
            outcome = ApprovalOutcome.AdvancedToNextLevel;
        }
        else
        {
            request.Status = LeaveStatus.Approved;
            outcome = ApprovalOutcome.FullyApproved;
        }

        request.UpdatedAt = now;
        await _db.SaveChangesAsync();

        if (outcome == ApprovalOutcome.AdvancedToNextLevel)
        {
            await _notifications.NotifyApprovedNextAsync(request.Id);
        }
        else if (outcome == ApprovalOutcome.FullyApproved)
        {
            await _notifications.NotifyFullyApprovedAsync(request.Id);
        }

        return new ApprovalActionResult(true, null, outcome);
    }

    public async Task<ApprovalActionResult> RejectAsync(int stepId, int currentUserId, string comment)
    {
        if (string.IsNullOrWhiteSpace(comment))
        {
            return new ApprovalActionResult(false, ErrorRejectCommentRequired, ApprovalOutcome.None);
        }

        var step = await LoadStepForActionAsync(stepId);
        if (step is null)
        {
            return new ApprovalActionResult(false, ErrorStepNotFound, ApprovalOutcome.None);
        }

        var canAct = await EnsureCanActAsync(step, currentUserId);
        if (!canAct.Success)
        {
            return canAct;
        }

        var now = DateTime.UtcNow;
        var request = step.LeaveRequest;

        step.Decision = ApprovalDecision.Rejected;
        step.ApproverUserId = currentUserId;
        step.Comment = comment.Trim();
        step.DecidedAt = now;

        request.Status = LeaveStatus.Rejected;
        request.UpdatedAt = now;

        await _db.SaveChangesAsync();

        await _notifications.NotifyRejectedAsync(request.Id, step.Comment ?? string.Empty);

        return new ApprovalActionResult(true, null, ApprovalOutcome.Rejected);
    }

    /// <summary>
    /// 載入簽核動作需要的完整關聯資料。
    /// </summary>
    private Task<Models.Entities.LeaveRequestStep?> LoadStepForActionAsync(int stepId)
    {
        return _db.LeaveRequestSteps
            .Include(s => s.LeaveRequest)
                .ThenInclude(r => r.Student)
                    .ThenInclude(u => u.Cohort)
            .FirstOrDefaultAsync(s => s.Id == stepId);
    }

    /// <summary>
    /// 檢查「此使用者是否能對這個 Step 動作」。
    /// 規則：
    /// 1) 整張單必須仍在 Pending
    /// 2) Step.Level 必須等於 LeaveRequest.CurrentLevel
    /// 3) Step 尚未被簽核（Decision=Pending）
    /// 4) 使用者必須具備該 Step 角色
    /// 5) 使用者必須是該班期指定的該角色簽核人
    /// </summary>
    private async Task<ApprovalActionResult> EnsureCanActAsync(Models.Entities.LeaveRequestStep step, int currentUserId)
    {
        var request = step.LeaveRequest;

        if (request.Status != LeaveStatus.Pending)
        {
            return new ApprovalActionResult(false, ErrorRequestNotPending, ApprovalOutcome.None);
        }

        if (step.Level != request.CurrentLevel)
        {
            return new ApprovalActionResult(false, ErrorNotCurrentLevel, ApprovalOutcome.None);
        }

        if (step.Decision != ApprovalDecision.Pending)
        {
            return new ApprovalActionResult(false, ErrorAlreadyDecided, ApprovalOutcome.None);
        }

        var hasRequiredRole = await _db.UserRoles
            .Include(ur => ur.Role)
            .AnyAsync(ur => ur.UserId == currentUserId && ur.Role.Name == step.ApproverRole);

        if (!hasRequiredRole)
        {
            return new ApprovalActionResult(false, ErrorRoleMismatch, ApprovalOutcome.None);
        }

        var assignedApproverId = GetAssignedApproverId(step);
        if (!assignedApproverId.HasValue || assignedApproverId.Value != currentUserId)
        {
            return new ApprovalActionResult(false, ErrorNotAssignedApprover, ApprovalOutcome.None);
        }

        return new ApprovalActionResult(true, null, ApprovalOutcome.None);
    }

    /// <summary>
    /// 依關卡角色取得班期指定簽核人 UserId。
    /// </summary>
    private static int? GetAssignedApproverId(Models.Entities.LeaveRequestStep step)
    {
        var cohort = step.LeaveRequest.Student.Cohort;
        if (cohort is null)
        {
            return null;
        }

        return step.ApproverRole switch
        {
            RoleTutor => cohort.TutorUserId,
            RoleSectionChief => cohort.SectionChiefUserId,
            RoleBranchDirector => cohort.BranchDirectorUserId,
            _ => null
        };
    }

    private async Task<IReadOnlyList<PendingApprovalItem>> GetPendingCoreAsync(int currentUserId)
    {
        var roleNames = await _db.UserRoles
            .Include(ur => ur.Role)
            .Where(ur => ur.UserId == currentUserId)
            .Select(ur => ur.Role.Name)
            .ToListAsync();

        if (!roleNames.Any())
        {
            return Array.Empty<PendingApprovalItem>();
        }

        var candidates = await _db.LeaveRequestSteps
            .Include(s => s.LeaveRequest)
                .ThenInclude(r => r.Student)
                    .ThenInclude(u => u.Cohort)
            .Include(s => s.LeaveRequest)
                .ThenInclude(r => r.LeaveType)
            .Where(s =>
                s.Decision == ApprovalDecision.Pending &&
                s.LeaveRequest.Status == LeaveStatus.Pending &&
                s.Level == s.LeaveRequest.CurrentLevel &&
                roleNames.Contains(s.ApproverRole))
            .AsNoTracking()
            .ToListAsync();

        return candidates
            .Where(s => GetAssignedApproverId(s) == currentUserId)
            .OrderByDescending(s => s.LeaveRequest.CreatedAt)
            .Select(s => new PendingApprovalItem
            {
                LeaveRequestId = s.LeaveRequestId,
                StepId = s.Id,
                CurrentLevel = s.Level,
                StudentDisplayName = s.LeaveRequest.Student.DisplayName,
                LeaveTypeName = s.LeaveRequest.LeaveType.Name,
                StartAt = s.LeaveRequest.StartAt,
                EndAt = s.LeaveRequest.EndAt,
                TotalHours = s.LeaveRequest.TotalHours,
                CreatedAt = s.LeaveRequest.CreatedAt
            })
            .ToList();
    }

    private async Task<IReadOnlyList<ApprovalHistoryItem>> GetHistoryCoreAsync(int currentUserId)
    {
        return await _db.LeaveRequestSteps
            .Include(s => s.LeaveRequest)
                .ThenInclude(r => r.Student)
            .Include(s => s.LeaveRequest)
                .ThenInclude(r => r.LeaveType)
            .Where(s => s.ApproverUserId == currentUserId && s.Decision != ApprovalDecision.Pending)
            .OrderByDescending(s => s.DecidedAt)
            .AsNoTracking()
            .Select(s => new ApprovalHistoryItem
            {
                LeaveRequestId = s.LeaveRequestId,
                StepId = s.Id,
                Level = s.Level,
                StudentDisplayName = s.LeaveRequest.Student.DisplayName,
                LeaveTypeName = s.LeaveRequest.LeaveType.Name,
                Decision = s.Decision,
                Comment = s.Comment,
                DecidedAt = s.DecidedAt
            })
            .ToListAsync();
    }

    private async Task<ApprovalDetailViewModel?> GetDetailCoreAsync(int leaveRequestId, int currentUserId)
    {
        var request = await _db.LeaveRequests
            .Include(r => r.Student)
                .ThenInclude(u => u.Cohort)
            .Include(r => r.LeaveType)
            .Include(r => r.Steps)
                .ThenInclude(s => s.Approver)
            .FirstOrDefaultAsync(r => r.Id == leaveRequestId);

        if (request is null)
        {
            return null;
        }

        var currentStep = request.Steps.FirstOrDefault(s => s.Level == request.CurrentLevel);
        var canViewByCurrentAssignment = false;

        if (currentStep is not null)
        {
            var hasRole = await _db.UserRoles
                .Include(ur => ur.Role)
                .AnyAsync(ur => ur.UserId == currentUserId && ur.Role.Name == currentStep.ApproverRole);

            canViewByCurrentAssignment = hasRole && GetAssignedApproverId(currentStep) == currentUserId;
        }

        var canViewByHistory = request.Steps.Any(s => s.ApproverUserId == currentUserId);
        if (!canViewByCurrentAssignment && !canViewByHistory)
        {
            return null;
        }

        var canActOnCurrentStep =
            request.Status == LeaveStatus.Pending &&
            currentStep is not null &&
            currentStep.Decision == ApprovalDecision.Pending &&
            canViewByCurrentAssignment;

        return new ApprovalDetailViewModel
        {
            LeaveRequestId = request.Id,
            CurrentLevel = request.CurrentLevel,
            Status = request.Status,
            StudentDisplayName = request.Student.DisplayName,
            LeaveTypeName = request.LeaveType.Name,
            StartAt = request.StartAt,
            EndAt = request.EndAt,
            TotalHours = request.TotalHours,
            Reason = request.Reason,
            CanActOnCurrentStep = canActOnCurrentStep,
            CurrentStepId = canActOnCurrentStep ? currentStep!.Id : null,
            Steps = request.Steps
                .OrderBy(s => s.Level)
                .Select(s => new ApprovalStepViewModel
                {
                    StepId = s.Id,
                    Level = s.Level,
                    ApproverRole = s.ApproverRole,
                    Decision = s.Decision,
                    ApproverUserId = s.ApproverUserId,
                    ApproverDisplayName = s.Approver?.DisplayName,
                    Comment = s.Comment,
                    DecidedAt = s.DecidedAt
                })
                .ToList()
        };
    }
}
