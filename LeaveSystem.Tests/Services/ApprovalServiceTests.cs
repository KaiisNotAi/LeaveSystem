using LeaveSystem.Models.Enums;
using LeaveSystem.Services;
using LeaveSystem.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 4 簽核引擎的核心業務規則測試。
///
/// TDD 節奏：
///   Red   ← 先寫這裡的測試(此時 ApprovalService 還不存在,編譯不過)
///   Green ← 接著寫 ApprovalService 讓每一條測試逐一變綠
///   Refactor ← 收尾整理程式碼,測試仍需全綠
/// </summary>
public class ApprovalServiceTests
{
    [Fact]
    public async Task GetPendingForUserAsync_應只回傳我當關且Pending的單()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var tutorPending = await sut.GetPendingForUserAsync(ApprovalTestScenario.TutorUserId);
        Assert.Single(tutorPending);

        var item = tutorPending[0];
        Assert.Equal(ApprovalTestScenario.LeaveRequestId, item.LeaveRequestId);
        Assert.Equal(scenario.Step1TutorId, item.StepId);
        Assert.Equal(1, item.CurrentLevel);

        var sectionChiefPending = await sut.GetPendingForUserAsync(ApprovalTestScenario.SectionChiefUserId);
        Assert.Empty(sectionChiefPending);
    }

    [Fact]
    public async Task GetHistoryForUserAsync_應只回傳我簽過的Step()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var approve = await sut.ApproveAsync(
            scenario.Step1TutorId,
            ApprovalTestScenario.TutorUserId,
            comment: "同意");
        Assert.True(approve.Success, approve.ErrorMessage);

        var tutorHistory = await sut.GetHistoryForUserAsync(ApprovalTestScenario.TutorUserId);
        Assert.Single(tutorHistory);
        Assert.Equal(scenario.Step1TutorId, tutorHistory[0].StepId);
        Assert.Equal(ApprovalDecision.Approved, tutorHistory[0].Decision);

        var sectionChiefHistory = await sut.GetHistoryForUserAsync(ApprovalTestScenario.SectionChiefUserId);
        Assert.Empty(sectionChiefHistory);
    }

    [Fact]
    public async Task GetDetailForUserAsync_當關指派人_CanAct應為True()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var detail = await sut.GetDetailForUserAsync(
            ApprovalTestScenario.LeaveRequestId,
            ApprovalTestScenario.TutorUserId);

        Assert.NotNull(detail);
        Assert.True(detail!.CanActOnCurrentStep);
        Assert.Equal(scenario.Step1TutorId, detail.CurrentStepId);
        Assert.Equal(3, detail.Steps.Count);
    }

    [Fact]
    public async Task GetDetailForUserAsync_路人_應回傳Null()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var detail = await sut.GetDetailForUserAsync(
            ApprovalTestScenario.LeaveRequestId,
            ApprovalTestScenario.OutsiderUserId);

        Assert.Null(detail);
    }

    /// <summary>
    /// ❶ 有下一關時,核准應把 CurrentLevel 從 1 推進到 2,整張單保持 Pending,
    ///    當前 Step 應標記為 Approved 並記錄簽核者、簽核時間。
    /// </summary>
    [Fact]
    public async Task Approve_當有下一關_應推進CurrentLevel()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var result = await sut.ApproveAsync(
            stepId: scenario.Step1TutorId,
            currentUserId: ApprovalTestScenario.TutorUserId,
            comment: "同意");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(ApprovalOutcome.AdvancedToNextLevel, result.Outcome);

        var request = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Pending, request.Status);
        Assert.Equal(2, request.CurrentLevel);

        var step = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step1TutorId);
        Assert.Equal(ApprovalDecision.Approved, step.Decision);
        Assert.Equal(ApprovalTestScenario.TutorUserId, step.ApproverUserId);
        Assert.Equal("同意", step.Comment);
        Assert.NotNull(step.DecidedAt);
    }

    /// <summary>
    /// ❷ 逐關通過後,最後一關(分署長)核准時,整張單狀態應變為 Approved。
    /// </summary>
    [Fact]
    public async Task Approve_當是最後一關_應將LeaveRequestStatus設為Approved()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        // Step1 導師簽 → 推進到第 2 關
        var r1 = await sut.ApproveAsync(scenario.Step1TutorId, ApprovalTestScenario.TutorUserId, comment: null);
        Assert.True(r1.Success, r1.ErrorMessage);

        // Step2 科長簽 → 推進到第 3 關
        var r2 = await sut.ApproveAsync(scenario.Step2SectionChiefId, ApprovalTestScenario.SectionChiefUserId, comment: null);
        Assert.True(r2.Success, r2.ErrorMessage);

        // Step3 分署長簽 → 整張核准
        var r3 = await sut.ApproveAsync(scenario.Step3BranchDirectorId, ApprovalTestScenario.BranchDirectorUserId, comment: null);
        Assert.True(r3.Success, r3.ErrorMessage);
        Assert.Equal(ApprovalOutcome.FullyApproved, r3.Outcome);

        var request = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Approved, request.Status);
    }

    /// <summary>
    /// ❸ 駁回時整張單狀態變為 Rejected;後續關卡的 Step 保持 Pending(留痕,不再處理)。
    /// </summary>
    [Fact]
    public async Task Reject_應將LeaveRequestStatus設為Rejected()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var result = await sut.RejectAsync(
            stepId: scenario.Step1TutorId,
            currentUserId: ApprovalTestScenario.TutorUserId,
            comment: "資料不足,請補證明");

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(ApprovalOutcome.Rejected, result.Outcome);

        var request = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Rejected, request.Status);

        var step1 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step1TutorId);
        Assert.Equal(ApprovalDecision.Rejected, step1.Decision);
        Assert.Equal("資料不足,請補證明", step1.Comment);
        Assert.Equal(ApprovalTestScenario.TutorUserId, step1.ApproverUserId);
        Assert.NotNull(step1.DecidedAt);

        // 後續關卡的 Step 應保持 Pending(不被連動處理)
        var step2 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step2SectionChiefId);
        Assert.Equal(ApprovalDecision.Pending, step2.Decision);
    }

    /// <summary>
    /// ❹ 駁回時 Comment 為 null / 空白 / 純空格 → 應失敗,不改任何資料。
    /// </summary>
    [Fact]
    public async Task Reject_未填Comment_應失敗()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var result = await sut.RejectAsync(
            stepId: scenario.Step1TutorId,
            currentUserId: ApprovalTestScenario.TutorUserId,
            comment: "   ");

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        var request = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(LeaveStatus.Pending, request.Status);
        Assert.Equal(1, request.CurrentLevel);

        var step1 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step1TutorId);
        Assert.Equal(ApprovalDecision.Pending, step1.Decision);
        Assert.Null(step1.DecidedAt);
    }

    /// <summary>
    /// ❺ CurrentLevel=1 時,科長不能跨越去簽 Step2(Level=2)。
    /// </summary>
    [Fact]
    public async Task Approve_不屬於當前關卡的Step_應失敗()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var result = await sut.ApproveAsync(
            stepId: scenario.Step2SectionChiefId,
            currentUserId: ApprovalTestScenario.SectionChiefUserId,
            comment: null);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        var request = await scenario.Db.LeaveRequests.AsNoTracking()
            .FirstAsync(r => r.Id == ApprovalTestScenario.LeaveRequestId);
        Assert.Equal(1, request.CurrentLevel);

        var step2 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step2SectionChiefId);
        Assert.Equal(ApprovalDecision.Pending, step2.Decision);
    }

    /// <summary>
    /// ❻ 決策 2A:非該班期指派人 → 應失敗。
    ///    Step1 需要「導師」,且必須是該班期 Cohort.TutorUserId 指定的那個人。
    ///    路人甲(OutsiderUserId=999)雖然想簽,但既非導師角色、也非班期指派人。
    /// </summary>
    [Fact]
    public async Task Approve_非本角色或非班期指派人_應失敗()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        var result = await sut.ApproveAsync(
            stepId: scenario.Step1TutorId,
            currentUserId: ApprovalTestScenario.OutsiderUserId,
            comment: null);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));

        var step1 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step1TutorId);
        Assert.Equal(ApprovalDecision.Pending, step1.Decision);
        Assert.Null(step1.ApproverUserId);
    }

    /// <summary>
    /// ❼ 對已被簽核過(Decision != Pending)的 Step 再次操作 → 應失敗,避免覆寫歷史。
    /// </summary>
    [Fact]
    public async Task Approve_已簽核過的Step_應失敗()
    {
        using var scenario = await ApprovalTestScenario.CreateAsync();
        var sut = new ApprovalService(scenario.Db);

        // 導師先合法簽過 Step1
        var first = await sut.ApproveAsync(scenario.Step1TutorId, ApprovalTestScenario.TutorUserId, comment: "OK");
        Assert.True(first.Success, first.ErrorMessage);

        // 再簽一次 → 應被擋
        var second = await sut.ApproveAsync(scenario.Step1TutorId, ApprovalTestScenario.TutorUserId, comment: "再簽一次");

        Assert.False(second.Success);
        Assert.False(string.IsNullOrWhiteSpace(second.ErrorMessage));

        // 資料應維持第一次簽核的結果
        var step1 = await scenario.Db.LeaveRequestSteps.AsNoTracking()
            .FirstAsync(s => s.Id == scenario.Step1TutorId);
        Assert.Equal(ApprovalDecision.Approved, step1.Decision);
        Assert.Equal("OK", step1.Comment);
    }
}
