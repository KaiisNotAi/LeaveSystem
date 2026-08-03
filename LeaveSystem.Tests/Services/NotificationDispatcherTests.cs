using LeaveSystem.Services;
using LeaveSystem.Services.Notifications;
using LeaveSystem.Tests.Helpers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 7 通知派送規格測試。
/// 使用 ApprovalTestScenario 作為 InMemory DB 情境，
/// FakeEmailSender 驗證外送呼叫；NotificationService 為真實實作。
/// </summary>
public class NotificationDispatcherTests
{
    private static NotificationDispatcher CreateSut(
        Data.AppDbContext db,
        out INotificationService notifications,
        out FakeEmailSender emails)
    {
        notifications = new NotificationService(db);
        emails = new FakeEmailSender();
        return new NotificationDispatcher(db, notifications, emails, NullLogger<NotificationDispatcher>.Instance);
    }

    [Fact]
    public async Task NotifySubmittedAsync_應通知第一關簽核人_導師()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        // 補導師 email
        var tutor = await scenario.Db.UserAccounts.FirstAsync(u => u.Id == ApprovalTestScenario.TutorUserId);
        tutor.Email = "tutor@test.local";
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, out var notifications, out var emails);

        await sut.NotifySubmittedAsync(ApprovalTestScenario.LeaveRequestId);

        var tutorInbox = await notifications.GetForUserAsync(ApprovalTestScenario.TutorUserId);
        Assert.Single(tutorInbox);
        Assert.Contains("待簽核", tutorInbox[0].Title);

        Assert.Single(emails.Sent);
        Assert.Equal("tutor@test.local", emails.Sent[0].To);
    }

    [Fact]
    public async Task NotifyApprovedNextAsync_應通知當前CurrentLevel對應簽核人()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        // 手動將 CurrentLevel 推進到 2
        var req = await scenario.Db.LeaveRequests.FirstAsync();
        req.CurrentLevel = 2;
        await scenario.Db.SaveChangesAsync();

        var chief = await scenario.Db.UserAccounts.FirstAsync(u => u.Id == ApprovalTestScenario.SectionChiefUserId);
        chief.Email = "chief@test.local";
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, out var notifications, out var emails);

        await sut.NotifyApprovedNextAsync(ApprovalTestScenario.LeaveRequestId);

        var chiefInbox = await notifications.GetForUserAsync(ApprovalTestScenario.SectionChiefUserId);
        Assert.Single(chiefInbox);
        Assert.Single(emails.Sent);
        Assert.Equal("chief@test.local", emails.Sent[0].To);
    }

    [Fact]
    public async Task NotifyFullyApprovedAsync_應通知學員()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var student = await scenario.Db.UserAccounts.FirstAsync(u => u.Id == ApprovalTestScenario.StudentUserId);
        student.Email = "stu@test.local";
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, out var notifications, out var emails);

        await sut.NotifyFullyApprovedAsync(ApprovalTestScenario.LeaveRequestId);

        var stuInbox = await notifications.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        Assert.Single(stuInbox);
        Assert.Contains("核准", stuInbox[0].Title);

        Assert.Single(emails.Sent);
        Assert.Equal("stu@test.local", emails.Sent[0].To);
    }

    [Fact]
    public async Task NotifyRejectedAsync_應通知學員且訊息含駁回原因()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var student = await scenario.Db.UserAccounts.FirstAsync(u => u.Id == ApprovalTestScenario.StudentUserId);
        student.Email = "stu@test.local";
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, out var notifications, out var emails);

        await sut.NotifyRejectedAsync(ApprovalTestScenario.LeaveRequestId, "資料不足");

        var stuInbox = await notifications.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        Assert.Single(stuInbox);
        Assert.Contains("駁回", stuInbox[0].Title);
        Assert.Contains("資料不足", stuInbox[0].Message);

        Assert.Single(emails.Sent);
        Assert.Contains("資料不足", emails.Sent[0].Body);
    }

    [Fact]
    public async Task 使用者無Email時_只建立站內通知_不呼叫Email()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        // 不設定學員 email
        var sut = CreateSut(scenario.Db, out var notifications, out var emails);

        await sut.NotifyFullyApprovedAsync(ApprovalTestScenario.LeaveRequestId);

        var stuInbox = await notifications.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        Assert.Single(stuInbox);
        Assert.Empty(emails.Sent);
    }

    [Fact]
    public async Task Email發送擲例外_不應影響站內通知也不擲例外()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var student = await scenario.Db.UserAccounts.FirstAsync(u => u.Id == ApprovalTestScenario.StudentUserId);
        student.Email = "stu@test.local";
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, out var notifications, out var emails);
        emails.ThrowOnSend = true;

        // 不應擲例外
        await sut.NotifyFullyApprovedAsync(ApprovalTestScenario.LeaveRequestId);

        // 站內通知仍應存在
        var stuInbox = await notifications.GetForUserAsync(ApprovalTestScenario.StudentUserId);
        Assert.Single(stuInbox);
    }
}
