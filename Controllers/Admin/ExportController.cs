using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Services.Export;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 匯出報表（Phase 8）。
/// 路由：/Admin/Export/...
///
/// 提供某班期（個別學生或全部）的請假 + 曠課紀錄下載：
///   ‧ <see cref="ExportXlsx"/>：單一 xlsx，內含 3 個 sheet（請假明細 / 曠課明細 / 彙總）
///   ‧ <see cref="ExportLeavesCsv"/> / <see cref="ExportAbsencesCsv"/>：各自 CSV
///
/// 業務資料由 <see cref="ILeaveReportService"/> 與 <see cref="IAbsenceRecordService"/> 提供，
/// 匯出格式由 <see cref="ICsvExporter"/> / <see cref="IExcelExporter"/> 處理。
/// </summary>
[Route("Admin/Export/{action=Index}")]
[Authorize(Roles = "Staff,Admin")]
public class ExportController : Controller
{
    private readonly ILeaveReportService _leaveReport;
    private readonly IAbsenceRecordService _absence;
    private readonly ICsvExporter _csv;
    private readonly IExcelExporter _excel;

    public ExportController(
        ILeaveReportService leaveReport,
        IAbsenceRecordService absence,
        ICsvExporter csv,
        IExcelExporter excel)
    {
        _leaveReport = leaveReport;
        _absence = absence;
        _csv = csv;
        _excel = excel;
    }

    /// <summary>
    /// GET /Admin/Export：顯示篩選表單。
    /// 未選班期時只顯示表單；有選班期則額外顯示三顆匯出按鈕（都是新的 GET 請求）。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AdminExportQuery query)
    {
        query ??= new AdminExportQuery();
        var vm = new AdminExportPageViewModel
        {
            Query = query,
            CohortOptions = await _absence.GetCohortOptionsAsync(),
            StudentOptions = await _absence.GetStudentOptionsAsync(),
        };
        return View(vm);
    }

    /// <summary>GET /Admin/Export/ExportXlsx：匯出多 sheet Excel（請假明細 / 曠課明細 / 彙總）。</summary>
    [HttpGet]
    public async Task<IActionResult> ExportXlsx([FromQuery] AdminExportQuery query)
    {
        if (!ModelState.IsValid || !query.CohortId.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        var (leaves, absences, cohortName) = await LoadAllAsync(query);

        var sheets = new[]
        {
            ExportSheet.Create("請假明細", leaves, LeaveDetailColumns),
            ExportSheet.Create("曠課明細", absences, AbsenceDetailColumns),
            ExportSheet.Create("彙總", BuildSummaryRows(leaves, absences), SummaryColumns),
        };

        var bytes = _excel.ExportWorkbook(sheets);
        return File(bytes,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"admin-export-{Slug(cohortName)}-{DateTime.Today:yyyyMMdd}.xlsx");
    }

    /// <summary>GET /Admin/Export/ExportLeavesCsv：僅請假明細 CSV。</summary>
    [HttpGet]
    public async Task<IActionResult> ExportLeavesCsv([FromQuery] AdminExportQuery query)
    {
        if (!ModelState.IsValid || !query.CohortId.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        var leaves = await _leaveReport.GetLeaveDetailsForAdminAsync(query);
        var cohortName = await GetCohortNameAsync(query.CohortId!.Value);

        var bytes = _csv.Export(leaves, LeaveDetailColumns);
        return File(bytes, "text/csv; charset=utf-8",
            $"admin-export-leaves-{Slug(cohortName)}-{DateTime.Today:yyyyMMdd}.csv");
    }

    /// <summary>GET /Admin/Export/ExportAbsencesCsv：僅曠課明細 CSV。</summary>
    [HttpGet]
    public async Task<IActionResult> ExportAbsencesCsv([FromQuery] AdminExportQuery query)
    {
        if (!ModelState.IsValid || !query.CohortId.HasValue)
        {
            return RedirectToAction(nameof(Index));
        }

        var absenceVm = await _absence.GetForAdminAsync(MapToAbsenceQuery(query));
        var cohortName = await GetCohortNameAsync(query.CohortId!.Value);

        var bytes = _csv.Export(absenceVm.Items, AbsenceDetailColumns);
        return File(bytes, "text/csv; charset=utf-8",
            $"admin-export-absences-{Slug(cohortName)}-{DateTime.Today:yyyyMMdd}.csv");
    }

    // ─── 私有輔助 ─────────────────────────────────────────────

    private async Task<(IReadOnlyList<AdminLeaveDetailRow> Leaves, IReadOnlyList<AbsenceRecordListItem> Absences, string CohortName)>
        LoadAllAsync(AdminExportQuery query)
    {
        var leaves = await _leaveReport.GetLeaveDetailsForAdminAsync(query);
        var absenceVm = await _absence.GetForAdminAsync(MapToAbsenceQuery(query));
        var cohortName = await GetCohortNameAsync(query.CohortId!.Value);
        return (leaves, absenceVm.Items, cohortName);
    }

    private async Task<string> GetCohortNameAsync(int cohortId)
    {
        var options = await _absence.GetCohortOptionsAsync();
        return options.FirstOrDefault(c => c.Id == cohortId)?.Name ?? $"cohort{cohortId}";
    }

    private static AbsenceRecordAdminQuery MapToAbsenceQuery(AdminExportQuery q) => new()
    {
        CohortId = q.CohortId,
        StudentId = q.StudentId,
        DateFrom = q.DateFrom,
        DateTo = q.DateTo,
    };

    /// <summary>將班期名稱轉為安全檔名片段（保留中英數字與底線、連字號；其餘轉底線）。</summary>
    private static string Slug(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return "cohort";
        var chars = name.Select(ch =>
            char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_').ToArray();
        return new string(chars);
    }

    /// <summary>
    /// 依請假明細 + 曠課明細建立「每學員 × 假別」彙總列（含曠課視為一種假別）。
    /// 只算 Approved 請假時數。
    /// </summary>
    private static List<AdminExportSummaryRow> BuildSummaryRows(
        IReadOnlyList<AdminLeaveDetailRow> leaves,
        IReadOnlyList<AbsenceRecordListItem> absences)
    {
        var summary = new List<AdminExportSummaryRow>();

        // 請假：只算 Approved
        var byLeave = leaves
            .Where(l => l.Status == LeaveStatus.Approved)
            .GroupBy(l => new { l.CohortName, l.StudentDisplayName, l.LeaveTypeName })
            .Select(g => new AdminExportSummaryRow
            {
                CohortName = g.Key.CohortName,
                StudentDisplayName = g.Key.StudentDisplayName,
                Category = g.Key.LeaveTypeName,
                TotalHours = g.Sum(x => x.TotalHours),
                Count = g.Count(),
            });
        summary.AddRange(byLeave);

        // 曠課：獨立列（Category = "曠課"）
        var byAbsence = absences
            .GroupBy(a => new { a.CohortName, a.StudentDisplayName })
            .Select(g => new AdminExportSummaryRow
            {
                CohortName = g.Key.CohortName ?? string.Empty,
                StudentDisplayName = g.Key.StudentDisplayName,
                Category = "曠課",
                TotalHours = g.Sum(x => x.Hours),
                Count = g.Count(),
            });
        summary.AddRange(byAbsence);

        return summary
            .OrderBy(r => r.CohortName)
            .ThenBy(r => r.StudentDisplayName)
            .ThenBy(r => r.Category)
            .ToList();
    }

    // ─── 匯出欄位定義 ─────────────────────────────────────────

    private static readonly IReadOnlyList<ExportColumn<AdminLeaveDetailRow>> LeaveDetailColumns = new[]
    {
        new ExportColumn<AdminLeaveDetailRow>("單號", r => r.Id, ExportValueType.Number),
        new ExportColumn<AdminLeaveDetailRow>("申請日", r => r.CreatedAt, ExportValueType.Date),
        new ExportColumn<AdminLeaveDetailRow>("班期", r => r.CohortName),
        new ExportColumn<AdminLeaveDetailRow>("學員", r => r.StudentDisplayName),
        new ExportColumn<AdminLeaveDetailRow>("假別", r => r.LeaveTypeName),
        new ExportColumn<AdminLeaveDetailRow>("起", r => r.StartAt, ExportValueType.DateTime),
        new ExportColumn<AdminLeaveDetailRow>("迄", r => r.EndAt, ExportValueType.DateTime),
        new ExportColumn<AdminLeaveDetailRow>("時數", r => r.TotalHours, ExportValueType.Hours),
        new ExportColumn<AdminLeaveDetailRow>("狀態", r => DescribeStatus(r.Status)),
        new ExportColumn<AdminLeaveDetailRow>("目前簽核關", r => r.CurrentLevel, ExportValueType.Number),
        new ExportColumn<AdminLeaveDetailRow>("原因", r => r.Reason),
    };

    private static readonly IReadOnlyList<ExportColumn<AbsenceRecordListItem>> AbsenceDetailColumns = new[]
    {
        new ExportColumn<AbsenceRecordListItem>("紀錄編號", r => r.Id, ExportValueType.Number),
        new ExportColumn<AbsenceRecordListItem>("班期", r => r.CohortName ?? string.Empty),
        new ExportColumn<AbsenceRecordListItem>("學員", r => r.StudentDisplayName),
        new ExportColumn<AbsenceRecordListItem>("發生時間", r => r.OccurredAt, ExportValueType.DateTime),
        new ExportColumn<AbsenceRecordListItem>("時數", r => r.Hours, ExportValueType.Hours),
        new ExportColumn<AbsenceRecordListItem>("備註", r => r.Note ?? string.Empty),
        new ExportColumn<AbsenceRecordListItem>("登錄人員", r => r.CreatedByDisplayName),
        new ExportColumn<AbsenceRecordListItem>("登錄時間", r => r.CreatedAt, ExportValueType.DateTime),
    };

    private static readonly IReadOnlyList<ExportColumn<AdminExportSummaryRow>> SummaryColumns = new[]
    {
        new ExportColumn<AdminExportSummaryRow>("班期", r => r.CohortName),
        new ExportColumn<AdminExportSummaryRow>("學員", r => r.StudentDisplayName),
        new ExportColumn<AdminExportSummaryRow>("類別", r => r.Category),
        new ExportColumn<AdminExportSummaryRow>("時數合計", r => r.TotalHours, ExportValueType.Hours),
        new ExportColumn<AdminExportSummaryRow>("筆數", r => r.Count, ExportValueType.Number),
    };

    private static string DescribeStatus(LeaveStatus status) => status switch
    {
        LeaveStatus.Pending => "進行中",
        LeaveStatus.Approved => "已核准",
        LeaveStatus.Rejected => "已駁回",
        LeaveStatus.Cancelled => "已取消",
        _ => status.ToString(),
    };

    /// <summary>彙總 sheet 內部使用的暫時列型別。</summary>
    private sealed class AdminExportSummaryRow
    {
        public string CohortName { get; set; } = string.Empty;
        public string StudentDisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public decimal TotalHours { get; set; }
        public int Count { get; set; }
    }
}
