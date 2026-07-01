using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 簽核規則管理。
/// 路由：/Admin/ApprovalRules/...
///
/// 每筆規則描述「請假時數區間 → 所需簽核角色順序」。
/// RequiredRoles 以 JSON 字串儲存，透過 ApprovalRoleJson 轉換。
/// </summary>
[Route("Admin/ApprovalRules/{action=Index}/{id?}")]
[Authorize(Roles = "Admin,Staff")]
public class ApprovalRulesController : Controller
{
    private readonly AppDbContext _db;

    public ApprovalRulesController(AppDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var rules = await _db.ApprovalRules
            .OrderBy(r => r.MinHours)
            .AsNoTracking()
            .ToListAsync();

        return View(rules);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create()
    {
        return View(new ApprovalRuleCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ApprovalRuleCreateViewModel input)
    {
        NormalizeAndValidate(input.MinHours, input.MaxHours, input.SelectedRoles);
        if (!ModelState.IsValid) return View(input);

        var entity = new ApprovalRule
        {
            MinHours = input.MinHours,
            MaxHours = input.MaxHours,
            RequiredRoles = ApprovalRoleJson.Serialize(input.SelectedRoles),
            Description = input.Description
        };
        _db.ApprovalRules.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = "簽核規則已建立";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 編輯
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.ApprovalRules.FirstOrDefaultAsync(r => r.Id == id);
        if (entity is null) return NotFound();

        var vm = new ApprovalRuleEditViewModel
        {
            Id = entity.Id,
            MinHours = entity.MinHours,
            MaxHours = entity.MaxHours,
            SelectedRoles = ApprovalRoleJson.Deserialize(entity.RequiredRoles),
            Description = entity.Description
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ApprovalRuleEditViewModel input)
    {
        NormalizeAndValidate(input.MinHours, input.MaxHours, input.SelectedRoles);
        if (!ModelState.IsValid) return View(input);

        var entity = await _db.ApprovalRules.FirstOrDefaultAsync(r => r.Id == input.Id);
        if (entity is null) return NotFound();

        entity.MinHours = input.MinHours;
        entity.MaxHours = input.MaxHours;
        entity.RequiredRoles = ApprovalRoleJson.Serialize(input.SelectedRoles);
        entity.Description = input.Description;
        await _db.SaveChangesAsync();

        TempData["Success"] = "簽核規則已更新";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 刪除
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(int id)
    {
        var entity = await _db.ApprovalRules.FirstOrDefaultAsync(r => r.Id == id);
        if (entity is null) return NotFound();

        _db.ApprovalRules.Remove(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = "簽核規則已刪除";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // Helper：合理性檢查（區間、角色選項）
    // ─────────────────────────────────────────────────────────────
    private void NormalizeAndValidate(decimal min, decimal? max, List<string> roles)
    {
        // MaxHours 若有值需 >= MinHours
        if (max.HasValue && max.Value < min)
        {
            ModelState.AddModelError(nameof(ApprovalRuleCreateViewModel.MaxHours),
                "區間上限不可小於下限");
        }

        // 角色需在候選清單內
        var valid = ApprovalRoleCatalog.All.Select(x => x.Value).ToHashSet();
        if (roles.Any(r => !valid.Contains(r)))
        {
            ModelState.AddModelError(nameof(ApprovalRuleCreateViewModel.SelectedRoles),
                "選擇的角色不在允許清單內");
        }

        if (!roles.Any())
        {
            ModelState.AddModelError(nameof(ApprovalRuleCreateViewModel.SelectedRoles),
                "請至少選擇一個簽核角色");
        }
    }
}
