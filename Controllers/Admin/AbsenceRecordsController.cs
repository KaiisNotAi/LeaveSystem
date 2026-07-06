using System.Security.Claims;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 曠課紀錄管理（Phase 6）。
/// 路由：/Admin/AbsenceRecords/...
///
/// 業務規則見 <c>docs/01-需求說明.md §3.5</c>：
///   ‧ 曠課是事後行政登錄的事實，不走請假簽核流程
///   ‧ 只有 Admin / Staff 可以新增、修改、刪除；學員只能查閱自己的紀錄
///
/// 授權由 <see cref="AuthorizeAttribute"/> 決定；業務規則（如學員是否存在）
/// 集中於 <see cref="IAbsenceRecordService"/>，Controller 保持薄層。
/// </summary>
[Route("Admin/AbsenceRecords/{action=Index}/{id?}")]
[Authorize(Roles = "Admin,Staff")]
public class AbsenceRecordsController : Controller
{
    private readonly IAbsenceRecordService _service;

    public AbsenceRecordsController(IAbsenceRecordService service)
    {
        _service = service;
    }

    // ─────────────────────────────────────────────────────────────
    // 列表 + 篩選
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] AbsenceRecordAdminQuery query)
    {
        query ??= new AbsenceRecordAdminQuery();
        var vm = await _service.GetForAdminAsync(query);
        return View(vm);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new AbsenceRecordCreateInput
        {
            OccurredAt = DateTime.Today,
            Hours = 1,
            StudentOptions = await _service.GetStudentOptionsAsync()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(AbsenceRecordCreateInput input)
    {
        if (!ModelState.IsValid)
        {
            input.StudentOptions = await _service.GetStudentOptionsAsync();
            return View(input);
        }

        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Challenge();
        }

        var result = await _service.CreateAsync(input, userId.Value);
        if (!result.Success)
        {
            ModelState.AddModelError(nameof(input.StudentId), result.ErrorMessage ?? "新增失敗");
            input.StudentOptions = await _service.GetStudentOptionsAsync();
            return View(input);
        }

        TempData["Success"] = "已新增曠課紀錄";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 編輯
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var vm = await _service.GetForEditAsync(id);
        if (vm is null) return NotFound();
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(AbsenceRecordEditInput input)
    {
        if (!ModelState.IsValid)
        {
            input.StudentOptions = await _service.GetStudentOptionsAsync();
            return View(input);
        }

        var result = await _service.UpdateAsync(input);
        if (!result.Success)
        {
            ModelState.AddModelError(string.Empty, result.ErrorMessage ?? "更新失敗");
            input.StudentOptions = await _service.GetStudentOptionsAsync();
            return View(input);
        }

        TempData["Success"] = "已更新曠課紀錄";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 刪除
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _service.DeleteAsync(id);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "刪除失敗";
        }
        else
        {
            TempData["Success"] = "已刪除曠課紀錄";
        }
        return RedirectToAction(nameof(Index));
    }

    // ─── 輔助方法 ───

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }
}
