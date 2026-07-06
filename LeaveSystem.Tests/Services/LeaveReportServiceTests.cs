using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Tests.Helpers;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 5 紀錄查詢與時數統計的核心業務規則測試。
///
/// TDD 節奏：
///   Red   ← 先寫此處測試（此時 LeaveReportService 尚未存在，編譯不過）
///   Green ← 接著寫 LeaveReportService 讓每一條測試逐一變綠
///   Refactor ← 收尾整理程式碼，測試仍需全綠
///
/// 對應規格：<c>docs/04-請假與簽核流程.md §7</c>
/// 測試資料：<see cref="ReportTestScenario"/>（多筆多狀態學員請假單）
/// </summary>
public class LeaveReportServiceTests
{
    // ═════════════════════════════════════════════════════════════════
    // Test 1：狀態篩選
    //   資料：學員 A 的 6 張單中，Approved=4、Pending=1、Rejected=1
    //   同時驗證 RejectReason 從 Rejected Step.Comment 帶出
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetMyRequests_依狀態篩選_應只回傳符合條件資料()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        // 只看已核准（RA1, RA2, RA3, RA6）
        var approved = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { Status = LeaveStatus.Approved });

        Assert.Equal(4, approved.Count);
        Assert.All(approved, r => Assert.Equal(LeaveStatus.Approved, r.Status));

        // 只看送審中（RA4）
        var pending = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { Status = LeaveStatus.Pending });

        Assert.Single(pending);
        Assert.Equal(LeaveStatus.Pending, pending[0].Status);

        // 只看被駁回（RA5）—— 順便驗證 RejectReason 帶出來
        var rejected = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { Status = LeaveStatus.Rejected });

        Assert.Single(rejected);
        Assert.Equal(LeaveStatus.Rejected, rejected[0].Status);
        Assert.Equal("資料不足", rejected[0].RejectReason);
    }

    // ═════════════════════════════════════════════════════════════════
    // Test 2：假別篩選
    //   病假只有 RA2 一筆；事假有 RA1、RA3、RA5、RA6 共 4 筆
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetMyRequests_依假別篩選_應只回傳符合條件資料()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        // 只看病假
        var sick = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { LeaveTypeId = ReportTestScenario.LeaveTypeIdSick });

        Assert.Single(sick);
        Assert.Equal("病假", sick[0].LeaveTypeName);

        // 只看事假（含跨月的 RA6、被駁回的 RA5）
        var personal = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { LeaveTypeId = ReportTestScenario.LeaveTypeIdPersonal });

        Assert.Equal(4, personal.Count);
        Assert.All(personal, r => Assert.Equal("事假", r.LeaveTypeName));
    }

    // ═════════════════════════════════════════════════════════════════
    // Test 3：日期區間篩選
    //   6/1 ~ 6/30 應回傳 StartAt 落在 6 月的 3 筆：
    //     RA1(6/8), RA2(6/15), RA6(6/28 —— 開始日在 6 月即算)
    //   RA3(7/1) 不算；RA4(8/10)、RA5(8/20) 不算
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetMyRequests_依日期區間篩選_應只回傳區間內資料()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var juneStart = new DateTime(2026, 6, 1);
        var juneEnd = new DateTime(2026, 6, 30, 23, 59, 59);

        var june = await sut.GetMyRequestsAsync(
            ReportTestScenario.StudentUserIdA,
            new MyLeaveRequestQuery { DateFrom = juneStart, DateTo = juneEnd });

        Assert.Equal(3, june.Count);
        Assert.All(june, r => Assert.InRange(r.StartAt, juneStart, juneEnd));
    }

    // ═════════════════════════════════════════════════════════════════
    // Test 4：累計時數（只算 Approved）
    //   期望（學員 A）：
    //     事假 = RA1(4) + RA3(4) + RA6(20) = 28h, 3 筆
    //     病假 = RA2(8) = 8h, 1 筆
    //     公假 (RA4 Pending 不算)、事假 RA5 (Rejected 不算)
    //   額外驗證：不會撈到學員 B 的資料（若 Service 忘記加 studentId 過濾，
    //             學員 B 的病假 8h 會被算進來，測試會失敗）
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetMyLeaveTypeSummary_應正確累計各假別時數()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var summary = await sut.GetMyLeaveTypeSummaryAsync(ReportTestScenario.StudentUserIdA);

        // 只有事假、病假 有 Approved 資料；公假的 RA4 是 Pending，不列出
        Assert.Equal(2, summary.Items.Count);

        var personal = summary.Items.Single(i => i.LeaveTypeName == "事假");
        Assert.Equal(28m, personal.TotalHours);
        Assert.Equal(3, personal.RequestCount);

        var sick = summary.Items.Single(i => i.LeaveTypeName == "病假");
        Assert.Equal(8m, sick.TotalHours);
        Assert.Equal(1, sick.RequestCount);

        // 跨假別總計
        Assert.Equal(36m, summary.TotalApprovedHours);

        // 班期上限資訊：A 班 TotalHours=900, LeaveLimitPercent=10 → 90h
        Assert.Equal(90, summary.CohortLeaveLimitHours);
        Assert.Equal("A班-2026春", summary.CohortName);
    }

    // ═════════════════════════════════════════════════════════════════
    // Test 5：行政彙總報表（分組 = Cohort × Student × LeaveType × YearMonth）
    //   期望（只算 Approved），共 5 列：
    //     A班 × 小明 × 事假 × 2026-06 = 24h (RA1 + RA6 跨月, 2 筆)
    //     A班 × 小明 × 病假 × 2026-06 =  8h (RA2, 1 筆)
    //     A班 × 小明 × 事假 × 2026-07 =  4h (RA3, 1 筆)
    //     B班 × 小華 × 病假 × 2026-06 =  8h (RB1, 1 筆)
    //     B班 × 小華 × 事假 × 2026-07 =  4h (RB2, 1 筆)
    //
    //   關鍵驗證：
    //     ‧ 跨月的 RA6 (6/28 → 7/2, 20h) 應整筆算 6 月（而非拆分或算 7 月）
    //     ‧ Rejected/Pending 不算
    //     ‧ 帶 CohortId 篩選只回該班期資料
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetAdminReport_依班期學員假別月份彙總_應正確回傳()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        // 不帶任何篩選 → 全部 Approved 的彙總
        var report = await sut.GetAdminReportAsync(new AdminReportQuery());

        Assert.Equal(5, report.Rows.Count);

        // 跨月測試：RA6 (6/28 → 7/2, 20h) 應整筆歸入 6 月
        // 所以 A班+小明+事假+2026/6 = RA1(4) + RA6(20) = 24h, 共 2 筆
        var aPersonalJune = report.Rows.Single(r =>
            r.CohortName == "A班-2026春" &&
            r.LeaveTypeName == "事假" &&
            r.Year == 2026 && r.Month == 6);
        Assert.Equal(24m, aPersonalJune.TotalHours);
        Assert.Equal(2, aPersonalJune.RequestCount);

        // 帶篩選：只看 A 班
        var reportOnlyA = await sut.GetAdminReportAsync(new AdminReportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        Assert.Equal(3, reportOnlyA.Rows.Count);
        Assert.All(reportOnlyA.Rows, r => Assert.Equal("A班-2026春", r.CohortName));
    }

    // ═════════════════════════════════════════════════════════════════
    // Test 6：行政彙總報表 — 日期區間篩選
    //   只取 StartAt 落在 6/1 ~ 6/30（含）的資料 → 應剩 3 列：
    //     A班 × 小明 × 事假 × 2026-06 = 24h  (RA1 + RA6 跨月開始日 6/28)
    //     A班 × 小明 × 病假 × 2026-06 =  8h  (RA2)
    //     B班 × 小華 × 病假 × 2026-06 =  8h  (RB1)
    //
    //   兩個關鍵驗證：
    //     1) DateTo 只給日期（時間 00:00:00）時，Service 必須補到當日 23:59:59
    //        才能把 6/30 當天的資料也含進去（否則只給 6/30 00:00:00 會把當天資料切掉）。
    //        這裡直接用 RA2 (6/15) 與跨月的 RA6 (StartAt=6/28) 驗證日期邊界。
    //     2) 跨月的 RA6 因為 StartAt=6/28 在區間內，整筆 20h 都要算入 6 月。
    // ═════════════════════════════════════════════════════════════════
    [Fact]
    public async Task GetAdminReport_依日期區間篩選_應只回傳區間內資料()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        // 模擬 View 送來的 <input type="date">：時間都是 00:00:00
        var june = await sut.GetAdminReportAsync(new AdminReportQuery
        {
            DateFrom = new DateTime(2026, 6, 1),
            DateTo = new DateTime(2026, 6, 30) // 時間 00:00:00；Service 需自動補到 23:59:59
        });

        Assert.Equal(3, june.Rows.Count);
        Assert.All(june.Rows, r => Assert.Equal(6, r.Month));
        Assert.All(june.Rows, r => Assert.Equal(2026, r.Year));

        // 跨月的 RA6（StartAt=6/28）仍要算 6 月，事假合計 = 4 + 20 = 24h
        var aPersonalJune = june.Rows.Single(r =>
            r.CohortName == "A班-2026春" && r.LeaveTypeName == "事假");
        Assert.Equal(24m, aPersonalJune.TotalHours);
    }
}
