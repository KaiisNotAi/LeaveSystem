using System.Security.Claims;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers;

/// <summary>
/// 學員自查曠課紀錄（Phase 6）。
/// 路由：/AbsenceRecords
///
/// ── 授權策略 ──
/// - 僅 <c>Student</c> 角色；學員只能查閱自己的紀錄（Service 以登入者 Id 強制過濾，
///   即使前端試圖以 URL 傳其他 studentId 亦無效）。
/// - 行政 CRUD 走另一支 <see cref="Admin.AbsenceRecordsController"/>，權限與體例分離
///   （見 docs/01 §3.5、Phase 6 Q4）。
///
/// 業務邏輯（篩選、日期補到當日 23:59:59、終身總計）由
/// <see cref="IAbsenceRecordService.GetMineAsync"/> 承擔；本 Controller 只做
/// 「取 UserId → 呼叫服務 → 回 View」。
/// </summary>
[Route("AbsenceRecords/{action=Index}/{id?}")]
[Authorize(Roles = "Student")]
public class AbsenceRecordsController : Controller
{
    private readonly IAbsenceRecordService _service;

    public AbsenceRecordsController(IAbsenceRecordService service)
    {
        _service = service;
    }

    /// <summary>
    /// 學員個人曠課紀錄查詢頁（GET /AbsenceRecords 或 /AbsenceRecords/Index）。
    /// 表單以 GET 送出，讓查詢條件保留在 URL 上方便複製/加書籤。
    ///
    /// 註：<c>Program.cs</c> 為了行政後台把 <c>/Views/Admin/{1}/{0}.cshtml</c> 插入
    /// <see cref="Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions.ViewLocationFormats"/>
    /// 的最前面，會讓預設 view 尋找先命中 <c>Views/Admin/AbsenceRecords/Index.cshtml</c>
    /// （模型型別不同會直接失敗）。因此這裡以絕對路徑明確指定學員自查頁。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] MyAbsenceRecordQuery query)
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
        {
            return Forbid();
        }

        query ??= new MyAbsenceRecordQuery();
        var vm = await _service.GetMineAsync(studentId.Value, query);
        return View("~/Views/AbsenceRecords/Index.cshtml", vm);
    }

    // ─── 輔助方法 ───

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }
}
