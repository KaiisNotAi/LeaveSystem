using System.Security.Claims;
using LeaveSystem.Controllers;
using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Services.Notifications;
using LeaveSystem.Tests.Helpers;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Tests.Controllers;

/// <summary>
/// 學員請假申請 Controller 針對 Edit / Cancel 的規格測試。
///
/// 「尚未簽核」的判定：Status == Pending 且所有 Steps.Decision == Pending。
/// 只要任一 Step 已核准，即禁止編輯與取消。
/// </summary>
public class LeaveRequestsControllerTests
{
    // ─── 假的依賴：Calculator ───

    private sealed class FakeLeaveCalculator : ILeaveCalculator
    {
        public decimal NextTotalHours { get; set; } = 30m;
        public bool NextSuccess { get; set; } = true;
        public string? NextError { get; set; }

        public bool TryCalculate(DateTime startAt, DateTime endAt, out decimal totalHours, out string? errorMessage)
        {
            totalHours = NextTotalHours;
            errorMessage = NextError;
            return NextSuccess;
        }
    }

    // ─── 假的依賴：Notifications ───

    private sealed class FakeNotificationDispatcher : INotificationDispatcher
    {
        public List<int> Submitted { get; } = new();
        public List<int> ApprovedNext { get; } = new();
        public List<int> FullyApproved { get; } = new();
        public List<(int Id, string Comment)> Rejected { get; } = new();
        public List<int> Cancelled { get; } = new();

        public Task NotifySubmittedAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        {
            Submitted.Add(leaveRequestId);
            return Task.CompletedTask;
        }

        public Task NotifyApprovedNextAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        {
            ApprovedNext.Add(leaveRequestId);
            return Task.CompletedTask;
        }

        public Task NotifyFullyApprovedAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        {
            FullyApproved.Add(leaveRequestId);
            return Task.CompletedTask;
        }

        public Task NotifyRejectedAsync(int leaveRequestId, string rejectComment, CancellationToken cancellationToken = default)
        {
            Rejected.Add((leaveRequestId, rejectComment));
            return Task.CompletedTask;
        }

        public Task NotifyCancelledAsync(int leaveRequestId, CancellationToken cancellationToken = default)
        {
            Cancelled.Add(leaveRequestId);
            return Task.CompletedTask;
        }
    }

    // ─── Helpers ───

    private static LeaveRequestsController CreateSut(
        AppDbContext db,
        int currentUserId,
        FakeLeaveCalculator? calculator = null,
        FakeNotificationDispatcher? notifications = null)
    {
        var controller = new LeaveRequestsController(
            db,
            calculator ?? new FakeLeaveCalculator(),
            notifications ?? new FakeNotificationDispatcher());

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, currentUserId.ToString()),
            new Claim(ClaimTypes.Role, "Student")
        }, authenticationType: "TestAuth");
        controller.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext { User = new ClaimsPrincipal(identity) }
        };
        // TempData 需要 provider 才不會爆
        controller.TempData = new Microsoft.AspNetCore.Mvc.ViewFeatures.TempDataDictionary(
            controller.HttpContext,
            new FakeTempDataProvider());
        return controller;
    }

    private sealed class FakeTempDataProvider : Microsoft.AspNetCore.Mvc.ViewFeatures.ITempDataProvider
    {
        private readonly Dictionary<string, object> _store = new();
        public IDictionary<string, object> LoadTempData(HttpContext context) => _store;
        public void SaveTempData(HttpContext context, IDictionary<string, object> values)
        {
            _store.Clear();
            foreach (var kv in values) _store[kv.Key] = kv.Value;
        }
    }

    /// <summary>
    /// 種入預設三檔 ApprovalRules（與 SeedData 一致）。
    /// </summary>
    private static async Task SeedApprovalRulesAsync(AppDbContext db)
    {
        db.ApprovalRules.AddRange(
            new ApprovalRule { MinHours = 0m,     MaxHours = 8m,   RequiredRoles = ApprovalRoleJson.Serialize(new[] { "Tutor" }) },
            new ApprovalRule { MinHours = 8.01m,  MaxHours = 24m,  RequiredRoles = ApprovalRoleJson.Serialize(new[] { "Tutor", "SectionChief" }) },
            new ApprovalRule { MinHours = 24.01m, MaxHours = null, RequiredRoles = ApprovalRoleJson.Serialize(new[] { "Tutor", "SectionChief", "BranchDirector" })}
        );
        await db.SaveChangesAsync();
    }

    // ═════════════════════════════════════════
    // Edit (GET)
    // ═════════════════════════════════════════

    [Fact]
    public async Task Edit_GET_擁有者且全部Pending_應回View並帶ViewModel()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId);

        var result = await sut.Edit(ApprovalTestScenario.LeaveRequestId);

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<LeaveRequestEditViewModel>(view.Model);
        Assert.Equal(ApprovalTestScenario.LeaveRequestId, vm.Id);
        Assert.Equal(ApprovalTestScenario.LeaveTypeId, vm.LeaveTypeId);
    }

    [Fact]
    public async Task Edit_GET_非擁有者_應回NotFound()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.OutsiderUserId);

        var result = await sut.Edit(ApprovalTestScenario.LeaveRequestId);

        Assert.IsType<NotFoundResult>(result);
    }

    [Fact]
    public async Task Edit_GET_已有Step被核准_應導回Index並帶錯誤訊息()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        // 讓第一關已通過
        var step1 = await scenario.Db.LeaveRequestSteps.FirstAsync(s => s.Level == 1);
        step1.Decision = ApprovalDecision.Approved;
        step1.DecidedAt = DateTime.UtcNow;
        await scenario.Db.SaveChangesAsync();

        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId);

        var result = await sut.Edit(ApprovalTestScenario.LeaveRequestId);

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
        Assert.True(sut.TempData.ContainsKey("Error"));
    }

    // ═════════════════════════════════════════
    // Edit (POST)
    // ═════════════════════════════════════════

    private static LeaveRequestEditViewModel BuildEditInput(decimal totalHoursHint)
        => new LeaveRequestEditViewModel
        {
            Id = ApprovalTestScenario.LeaveRequestId,
            LeaveTypeId = ApprovalTestScenario.LeaveTypeId,
            StartDate = new DateTime(2026, 6, 8),
            StartHour = 8,
            EndDate = new DateTime(2026, 6, 8),
            EndHour = 17,
            Reason = "修改後的原因"
        };

    [Fact]
    public async Task Edit_POST_時數減少導致規則變動_應重建Steps且CurrentLevel為1並發通知()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        var calc = new FakeLeaveCalculator { NextTotalHours = 6m }; // → 落入 0~8：只需 Tutor 一關
        var notif = new FakeNotificationDispatcher();
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId, calc, notif);

        var result = await sut.Edit(BuildEditInput(6m));

        Assert.IsType<RedirectToActionResult>(result);

        var reloaded = await scenario.Db.LeaveRequests
            .Include(r => r.Steps)
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(6m, reloaded.TotalHours);
        Assert.Equal(1, reloaded.CurrentLevel);
        Assert.Equal(LeaveStatus.Pending, reloaded.Status);
        Assert.Single(reloaded.Steps);
        Assert.Equal("Tutor", reloaded.Steps.First().ApproverRole);
        Assert.All(reloaded.Steps, s => Assert.Equal(ApprovalDecision.Pending, s.Decision));

        Assert.Single(notif.Submitted);
        Assert.Equal(ApprovalTestScenario.LeaveRequestId, notif.Submitted[0]);
    }

    [Fact]
    public async Task Edit_POST_時數不變_Steps保留且CurrentLevel不變()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        var originalStepIds = await scenario.Db.LeaveRequestSteps
            .Where(s => s.LeaveRequestId == ApprovalTestScenario.LeaveRequestId)
            .OrderBy(s => s.Level).Select(s => s.Id).ToListAsync();

        var calc = new FakeLeaveCalculator { NextTotalHours = 30m }; // 仍在 24.01~ 3 關規則
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId, calc);

        var result = await sut.Edit(BuildEditInput(30m));

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await scenario.Db.LeaveRequests
            .Include(r => r.Steps)
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        var newIds = reloaded.Steps.OrderBy(s => s.Level).Select(s => s.Id).ToList();
        Assert.Equal(originalStepIds, newIds); // 未被替換
    }

    [Fact]
    public async Task Edit_POST_超過班期上限_應回View且不變更資料()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);

        // 班期上限 = 900 * 10% = 90h。塞入其他已核准 85h，本單想改成 20h → 合計超過。
        scenario.Db.LeaveRequests.Add(new LeaveRequest
        {
            StudentId = ApprovalTestScenario.StudentUserId,
            LeaveTypeId = ApprovalTestScenario.LeaveTypeId,
            StartAt = new DateTime(2026, 5, 1, 8, 0, 0),
            EndAt = new DateTime(2026, 5, 20, 17, 0, 0),
            TotalHours = 85m,
            Reason = "先前的核准單",
            Status = LeaveStatus.Approved,
            CurrentLevel = 3,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        });
        await scenario.Db.SaveChangesAsync();

        var calc = new FakeLeaveCalculator { NextTotalHours = 20m };
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId, calc);

        var result = await sut.Edit(BuildEditInput(20m));

        Assert.IsType<ViewResult>(result);
        Assert.False(sut.ModelState.IsValid);

        var reloaded = await scenario.Db.LeaveRequests
            .AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(30m, reloaded.TotalHours); // 未變更
    }

    [Fact]
    public async Task Edit_POST_非擁有者_應回NotFound()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        await SeedApprovalRulesAsync(scenario.Db);
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.OutsiderUserId);

        var result = await sut.Edit(BuildEditInput(6m));

        Assert.IsType<NotFoundResult>(result);
    }

    // ═════════════════════════════════════════
    // Cancel (POST)
    // ═════════════════════════════════════════

    [Fact]
    public async Task Cancel_POST_擁有者且全部Pending_應軟刪除並保留Steps且發通知()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var notif = new FakeNotificationDispatcher();
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId, notifications: notif);

        var result = await sut.Cancel(ApprovalTestScenario.LeaveRequestId);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await scenario.Db.LeaveRequests
            .Include(r => r.Steps)
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Cancelled, reloaded.Status);
        Assert.Equal(3, reloaded.Steps.Count); // Steps 保留稽核

        Assert.Single(notif.Cancelled);
        Assert.Equal(ApprovalTestScenario.LeaveRequestId, notif.Cancelled[0]);
    }

    [Fact]
    public async Task Cancel_POST_已有Step被核准_應拒絕並保持狀態()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var step1 = await scenario.Db.LeaveRequestSteps.FirstAsync(s => s.Level == 1);
        step1.Decision = ApprovalDecision.Approved;
        step1.DecidedAt = DateTime.UtcNow;
        await scenario.Db.SaveChangesAsync();

        var notif = new FakeNotificationDispatcher();
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.StudentUserId, notifications: notif);

        var result = await sut.Cancel(ApprovalTestScenario.LeaveRequestId);

        Assert.IsType<RedirectToActionResult>(result);
        var reloaded = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Pending, reloaded.Status);
        Assert.Empty(notif.Cancelled);
        Assert.True(sut.TempData.ContainsKey("Error"));
    }

    [Fact]
    public async Task Cancel_POST_非擁有者_應回NotFound()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = CreateSut(scenario.Db, ApprovalTestScenario.OutsiderUserId);

        var result = await sut.Cancel(ApprovalTestScenario.LeaveRequestId);

        Assert.IsType<NotFoundResult>(result);
    }
}
