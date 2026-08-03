using LeaveSystem.Services;
using LeaveSystem.Services.Notifications;
using LeaveSystem.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 7 整合測試：ApprovalService 動作應觸發通知。
/// 使用 ApprovalTestScenario + 真實 NotificationService + FakeEmailSender。
/// </summary>
public class ApprovalNotificationIntegrationTests
{
    private static (ApprovalService approval, INotificationService notif, FakeEmailSender emails) Build(Data.AppDbContext db)
    {
        var notif = new NotificationService(db);
        var emails = new FakeEmailSender();
        var dispatcher = new NotificationDispatcher(db, notif, emails, NullLogger<NotificationDispatcher>.Instance);
        var approval = new ApprovalService(db, dispatcher);
        return (approval, notif, emails);
    }

    [Fact]
    public async Task Approve_下一關_應通知下一關簽核人()
    {
        using var s = await ApprovalTestScenario.CreateAsync();
        var (approval, notif, _) = Build(s.Db);

        var r = await approval.ApproveAsync(s.Step1TutorId, ApprovalTestScenario.TutorUserId, "ok");
        Assert.True(r.Success);

        var chiefInbox = await notif.GetForUserAsync(ApprovalTestScenario.SectionChiefUserId);
        Assert.Single(chiefInbox);
    }

    [Fact]
    public async Task Approve_最後一關_應通知學員全流程通過()
    {
        using var s = await ApprovalTestScenario.CreateAsync();
        var (approval, notif, _) = Build(s.Db);

        await approval.ApproveAsync(s.Step1TutorId, ApprovalTestScenario.TutorUserId, null);
        await approval.ApproveAsync(s.Step2SectionChiefId, ApprovalTestScenario.SectionChiefUserId, null);
        var r3 = await approval.ApproveAsync(s.Step3BranchDirectorId, ApprovalTestScenario.BranchDirectorUserId, null);
        Assert.Equal(ApprovalOutcome.FullyApproved, r3.Outcome);

        var stuInbox = await notif.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        // 只有全流程通過通知一則；中間關卡通知的是簽核人不是學員
        Assert.Single(stuInbox);
        Assert.Contains("核准", stuInbox[0].Title);
    }

    [Fact]
    public async Task Reject_應通知學員含駁回原因()
    {
        using var s = await ApprovalTestScenario.CreateAsync();
        var (approval, notif, _) = Build(s.Db);

        var r = await approval.RejectAsync(s.Step1TutorId, ApprovalTestScenario.TutorUserId, "資料不足");
        Assert.True(r.Success);

        var stuInbox = await notif.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        Assert.Single(stuInbox);
        Assert.Contains("駁回", stuInbox[0].Title);
        Assert.Contains("資料不足", stuInbox[0].Message);
    }
}
