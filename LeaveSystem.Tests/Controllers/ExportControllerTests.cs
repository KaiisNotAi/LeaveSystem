using System.Reflection;
using ClosedXML.Excel;
using LeaveSystem.Controllers.Admin;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Services.Export;
using LeaveSystem.Tests.Helpers;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Tests.Controllers;

/// <summary>
/// Phase 8 — ExportController 單元/整合測試。
/// 用 <see cref="ReportTestScenario"/> 提供請假資料；曠課無資料時服務會回空清單，
/// 匯出流程仍應成功產出檔案（多 sheet xlsx / CSV）。
/// </summary>
public class ExportControllerTests
{
    private static ExportController BuildSut(ReportTestScenario scenario)
    {
        var leaveReport = new LeaveReportService(scenario.Db);
        var absence = new AbsenceRecordService(scenario.Db);
        var csv = new CsvExporter();
        var excel = new ClosedXmlExcelExporter();
        return new ExportController(leaveReport, absence, csv, excel);
    }

    [Fact]
    public void Controller_ShouldRequireStaffOrAdmin()
    {
        var attr = typeof(ExportController).GetCustomAttribute<AuthorizeAttribute>();
        Assert.NotNull(attr);
        Assert.Equal("Staff,Admin", attr!.Roles);
    }

    [Fact]
    public async Task Index_ShouldReturnViewWithPageViewModel()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.Index(new AdminExportQuery());

        var view = Assert.IsType<ViewResult>(result);
        var vm = Assert.IsType<AdminExportPageViewModel>(view.Model);
        Assert.NotEmpty(vm.CohortOptions);
    }

    [Fact]
    public async Task ExportXlsx_WithoutCohortId_ShouldRedirectToIndex()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.ExportXlsx(new AdminExportQuery { CohortId = null });

        var redirect = Assert.IsType<RedirectToActionResult>(result);
        Assert.Equal("Index", redirect.ActionName);
    }

    [Fact]
    public async Task ExportXlsx_WithCohortId_ShouldReturnXlsxWithThreeSheets()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.ExportXlsx(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", file.ContentType);
        Assert.EndsWith(".xlsx", file.FileDownloadName);

        using var wb = new XLWorkbook(new MemoryStream(file.FileContents));
        Assert.Equal(3, wb.Worksheets.Count);
        Assert.NotNull(wb.Worksheet("請假明細"));
        Assert.NotNull(wb.Worksheet("曠課明細"));
        Assert.NotNull(wb.Worksheet("彙總"));

        // 請假明細至少有標題列 + 學員 A 的 6 張單
        var leaveSheet = wb.Worksheet("請假明細");
        Assert.Equal("單號", leaveSheet.Cell(1, 1).GetString());
        Assert.True(leaveSheet.LastRowUsed()!.RowNumber() >= 7);
    }

    [Fact]
    public async Task ExportLeavesCsv_ShouldReturnUtf8BomCsv()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.ExportLeavesCsv(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.EndsWith(".csv", file.FileDownloadName);
        Assert.Contains("admin-export-leaves-", file.FileDownloadName);
        // UTF-8 BOM
        Assert.True(file.FileContents.Length >= 3);
        Assert.Equal(0xEF, file.FileContents[0]);
        Assert.Equal(0xBB, file.FileContents[1]);
        Assert.Equal(0xBF, file.FileContents[2]);
    }

    [Fact]
    public async Task ExportAbsencesCsv_WithNoAbsenceData_ShouldStillReturnCsvWithHeaderOnly()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.ExportAbsencesCsv(new AdminExportQuery
        {
            CohortId = ReportTestScenario.CohortIdA
        });

        var file = Assert.IsType<FileContentResult>(result);
        Assert.Equal("text/csv; charset=utf-8", file.ContentType);
        Assert.Contains("admin-export-absences-", file.FileDownloadName);
    }

    [Fact]
    public async Task ExportLeavesCsv_WithoutCohortId_ShouldRedirect()
    {
        using var s = await ReportTestScenario.CreateAsync();
        var sut = BuildSut(s);

        var result = await sut.ExportLeavesCsv(new AdminExportQuery { CohortId = null });
        Assert.IsType<RedirectToActionResult>(result);
    }
}
