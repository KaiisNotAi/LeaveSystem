using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 班期管理。
/// 路由：/Admin/Cohorts/...
///
/// 每個班期記錄開課日期、結訓日期、總上課時數、請假上限百分比，
/// 以及對應的導師、科長、分署長（做為簽核派送依據）。
/// </summary>
[Route("Admin/Cohorts/{action=Index}/{id?}")]
[Authorize(Roles = "Admin,Staff")]
public class CohortsController : Controller
{
    private readonly AppDbContext _db;

    public CohortsController(AppDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var cohorts = await _db.Cohorts
            .Include(c => c.Tutor)
            .Include(c => c.SectionChief)
            .Include(c => c.BranchDirector)
            .OrderByDescending(c => c.StartDate)
            .AsNoTracking()
            .ToListAsync();

        return View(cohorts);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new CohortCreateViewModel();
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CohortCreateViewModel input)
    {
        // 步驟 1：基本欄位驗證
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }

        // 步驟 2：日期合理性檢查
        if (input.EndDate < input.StartDate)
        {
            ModelState.AddModelError(nameof(input.EndDate), "結訓日期不可早於開課日期");
            await PopulateOptionsAsync(input);
            return View(input);
        }

        // 步驟 3：轉成 Entity 存檔
        var cohort = new Cohort
        {
            Name = input.Name,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            TotalHours = input.TotalHours,
            LeaveLimitPercent = input.LeaveLimitPercent,
            TutorUserId = input.TutorUserId,
            SectionChiefUserId = input.SectionChiefUserId,
            BranchDirectorUserId = input.BranchDirectorUserId
        };

        _db.Cohorts.Add(cohort);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"班期「{cohort.Name}」已建立";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 編輯
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var cohort = await _db.Cohorts.FirstOrDefaultAsync(c => c.Id == id);
        if (cohort is null) return NotFound();

        var vm = new CohortEditViewModel
        {
            Id = cohort.Id,
            Name = cohort.Name,
            StartDate = cohort.StartDate,
            EndDate = cohort.EndDate,
            TotalHours = cohort.TotalHours,
            LeaveLimitPercent = cohort.LeaveLimitPercent,
            TutorUserId = cohort.TutorUserId,
            SectionChiefUserId = cohort.SectionChiefUserId,
            BranchDirectorUserId = cohort.BranchDirectorUserId
        };
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(CohortEditViewModel input)
    {
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }

        if (input.EndDate < input.StartDate)
        {
            ModelState.AddModelError(nameof(input.EndDate), "結訓日期不可早於開課日期");
            await PopulateOptionsAsync(input);
            return View(input);
        }

        var cohort = await _db.Cohorts.FirstOrDefaultAsync(c => c.Id == input.Id);
        if (cohort is null) return NotFound();

        cohort.Name = input.Name;
        cohort.StartDate = input.StartDate;
        cohort.EndDate = input.EndDate;
        cohort.TotalHours = input.TotalHours;
        cohort.LeaveLimitPercent = input.LeaveLimitPercent;
        cohort.TutorUserId = input.TutorUserId;
        cohort.SectionChiefUserId = input.SectionChiefUserId;
        cohort.BranchDirectorUserId = input.BranchDirectorUserId;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"班期「{cohort.Name}」已更新";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 刪除（有學員的班期不可刪除）
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var cohort = await _db.Cohorts
            .Include(c => c.Students)
            .FirstOrDefaultAsync(c => c.Id == id);
        if (cohort is null) return NotFound();

        if (cohort.Students.Any())
        {
            TempData["Error"] = $"班期「{cohort.Name}」下仍有 {cohort.Students.Count} 位學員，無法刪除。請先將學員移除或改指派其他班期。";
            return RedirectToAction(nameof(Index));
        }

        _db.Cohorts.Remove(cohort);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"班期「{cohort.Name}」已刪除";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // Helper：載入下拉選單資料
    // ─────────────────────────────────────────────────────────────
    private async Task PopulateOptionsAsync(CohortCreateViewModel vm)
    {
        vm.TutorOptions = await LoadUsersByRoleAsync("Tutor");
        vm.SectionChiefOptions = await LoadUsersByRoleAsync("SectionChief");
        vm.BranchDirectorOptions = await LoadUsersByRoleAsync("BranchDirector");
    }

    private async Task PopulateOptionsAsync(CohortEditViewModel vm)
    {
        vm.TutorOptions = await LoadUsersByRoleAsync("Tutor");
        vm.SectionChiefOptions = await LoadUsersByRoleAsync("SectionChief");
        vm.BranchDirectorOptions = await LoadUsersByRoleAsync("BranchDirector");
    }

    /// <summary>
    /// 依角色名稱取得可用使用者清單（只回啟用中的帳號）。
    /// </summary>
    private async Task<List<UserOption>> LoadUsersByRoleAsync(string roleName)
    {
        return await _db.UserAccounts
            .Where(u => u.IsActive && u.UserRoles.Any(ur => ur.Role!.Name == roleName))
            .OrderBy(u => u.DisplayName)
            .Select(u => new UserOption
            {
                Id = u.Id,
                DisplayName = u.DisplayName,
                Username = u.Username
            })
            .ToListAsync();
    }
}
