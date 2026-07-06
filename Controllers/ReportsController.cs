using System.Security.Claims;
using LeaveSystem.Data;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers;

/// <summary>
/// 紀錄查詢與時數統計（Phase 5）。
/// 路由：/Reports/...
///
/// ── 授權策略 ──
/// - <see cref="Index"/> / <see cref="Summary"/>：學員本人的自我查詢，需 <c>Student</c> 角色。
/// - <see cref="Admin"/>：跨學員行政報表，僅 <c>Staff</c> / <c>Admin</c> 可讀（見 docs/04 §7.4）。
///
/// 業務邏輯與資料統計皆由 <see cref="ILeaveReportService"/> 負責；
/// 本 Controller 只做「取 UserId → 呼叫服務 → 補下拉選項 → 回 View」。
/// </summary>
[Route("Reports/{action=Index}/{id?}")]
[Authorize]
public class ReportsController : Controller
{
    private readonly ILeaveReportService _reportService;
    private readonly AppDbContext _db;

    public ReportsController(ILeaveReportService reportService, AppDbContext db)
    {
        _reportService = reportService;
        _db = db;
    }

    /// <summary>
    /// 學員個人紀錄查詢頁（GET /Reports 或 /Reports/Index）。
    /// 表單以 GET 送出，讓查詢條件保留在 URL 上方便複製/加書籤。
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Index(MyLeaveRequestQuery query)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
        {
            return Forbid();
        }

        // 補假別下拉（讓表單能綁 dropdown 並回填使用者選擇）
        query.LeaveTypeOptions = await LoadLeaveTypeOptionsAsync();

        var items = await _reportService.GetMyRequestsAsync(studentId.Value, query);

        // 用 tuple ViewData 一起把 query + items 傳到 View，View 用強型別 items 做主表格
        ViewData["Query"] = query;
        return View(items);
    }

    /// <summary>
    /// 學員累計時數統計頁（GET /Reports/Summary）。
    /// 顯示各假別已核准時數、跨假別總計、班期上限進度。
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Student")]
    public async Task<IActionResult> Summary()
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
        {
            return Forbid();
        }

        var vm = await _reportService.GetMyLeaveTypeSummaryAsync(studentId.Value);
        return View(vm);
    }

    /// <summary>
    /// 行政彙總報表頁（GET /Reports/Admin）。
    /// 分組維度：Cohort × Student × LeaveType × YearMonth；只算 Approved（見 docs/04 §7）。
    /// </summary>
    [HttpGet]
    [Authorize(Roles = "Staff,Admin")]
    public async Task<IActionResult> Admin(AdminReportQuery query)
    {
        var vm = await _reportService.GetAdminReportAsync(query);
        return View(vm);
    }

    // ─── 輔助方法 ───

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }

    /// <summary>
    /// 學員查詢頁的假別下拉：只列出目前啟用的假別。
    /// </summary>
    private Task<List<LeaveTypeOption>> LoadLeaveTypeOptionsAsync()
    {
        return _db.LeaveTypes
            .AsNoTracking()
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Id)
            .Select(t => new LeaveTypeOption { Id = t.Id, Name = t.Name })
            .ToListAsync();
    }
}
