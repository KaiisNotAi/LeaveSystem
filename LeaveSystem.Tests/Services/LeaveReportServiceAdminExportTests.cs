using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Tests.Helpers;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 8 — 驗證 <see cref="LeaveReportService.GetLeaveDetailsForAdminAsync"/>：
///   ‧ 以 CohortId 為主 WHERE（跨學員但同班期）
///   ‧ StudentId 選填（進一步縮到單人）
///   ‧ 日期起迄套用在 StartAt；DateTo 補到當日 23:59:59
///   ‧ **全狀態**（Pending / Approved / Rejected 都要出現）
///   ‧ 排序：StartAt DESC
/// 情境資料見 <see cref="ReportTestScenario"/>。
/// </summary>
public class LeaveReportServiceAdminExportTests
{
    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_WithoutCohortId_ShouldReturnEmpty()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery { CohortId = null });

        Assert.Empty(rows);
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_ByCohortA_ShouldReturnAllStudentARequestsIncludingAllStatuses()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        // 情境：學員 A 共 6 張單（RA1..RA6），皆屬 CohortA
        Assert.Equal(6, rows.Count);
        // 全狀態都要在，不能只出 Approved
        Assert.Contains(rows, r => r.Status == LeaveStatus.Approved);
        Assert.Contains(rows, r => r.Status == LeaveStatus.Pending);
        Assert.Contains(rows, r => r.Status == LeaveStatus.Rejected);
        // 全部應屬 A 學員（跨學員但同班期 → 因為情境內 CohortA 只有一位學員）
        Assert.All(rows, r => Assert.Equal("小明", r.StudentDisplayName));
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_ByCohortB_ShouldReturnOnlyStudentBRequests()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdB
        });

        Assert.Equal(2, rows.Count);
        Assert.All(rows, r => Assert.Equal("小華", r.StudentDisplayName));
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_WithStudentIdFilter_ShouldNarrowToThatStudent()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA,
            StudentId = ReportTestScenario.StudentUserIdA
        });

        Assert.Equal(6, rows.Count);
        Assert.All(rows, r => Assert.Equal("小明", r.StudentDisplayName));
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_WithDateRangeInJuly_ShouldFilterByStartAt()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA,
            DateFrom = new DateTime(2026, 7, 1),
            DateTo = new DateTime(2026, 7, 31), // 用純日期 → 內部應補 23:59:59.9999
        });

        // 情境內 CohortA 於 2026-07 期間 StartAt 落點：僅 RA3 (2026-07-01)
        Assert.Single(rows);
        Assert.Equal(new DateTime(2026, 7, 1, 8, 0, 0), rows[0].StartAt);
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_ShouldOrderByStartAtDescending()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        for (var i = 1; i < rows.Count; i++)
        {
            Assert.True(rows[i - 1].StartAt >= rows[i].StartAt,
                $"Row {i - 1} StartAt {rows[i - 1].StartAt:o} should be >= row {i} StartAt {rows[i].StartAt:o}");
        }
    }

    [Fact]
    public async Task GetLeaveDetailsForAdminAsync_ShouldPopulateCohortNameAndLeaveTypeName()
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        Assert.All(rows, r =>
        {
            Assert.False(string.IsNullOrEmpty(r.CohortName));
            Assert.False(string.IsNullOrEmpty(r.LeaveTypeName));
        });
    }

    [Theory]
    [InlineData(LeaveStatus.Approved, 4)]  // 情境內 CohortA 已核准：RA1/RA2/RA3/RA6
    [InlineData(LeaveStatus.Pending, 1)]   // RA4
    [InlineData(LeaveStatus.Rejected, 1)]  // RA5
    public async Task GetLeaveDetailsForAdminAsync_WithStatusFilter_ShouldReturnOnlyMatchingRows(
        LeaveStatus status, int expectedCount)
    {
        using var scenario = await ReportTestScenario.CreateAsync();
        var sut = new LeaveReportService(scenario.Db);

        var rows = await sut.GetLeaveDetailsForAdminAsync(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA,
            Status = status
        });

        Assert.Equal(expectedCount, rows.Count);
        Assert.All(rows, r => Assert.Equal(status, r.Status));
    }
}
