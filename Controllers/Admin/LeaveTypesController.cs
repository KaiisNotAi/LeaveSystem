using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 假別管理。
/// 路由：/Admin/LeaveTypes/...
///
/// 假別採「停用」而非硬刪除，因為歷史請假紀錄會保留 LeaveTypeId，
/// 若真的刪除會造成資料完整性問題。
/// </summary>
[Route("Admin/LeaveTypes/{action=Index}/{id?}")]
[Authorize(Roles = "Admin,Staff")]
public class LeaveTypesController : Controller
{
    private readonly AppDbContext _db;

    public LeaveTypesController(AppDbContext db)
    {
        _db = db;
    }

    // ─────────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var types = await _db.LeaveTypes
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .AsNoTracking()
            .ToListAsync();

        return View(types);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public IActionResult Create()
    {
        return View(new LeaveTypeCreateViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeaveTypeCreateViewModel input)
    {
        if (!ModelState.IsValid) return View(input);

        // 代碼不可重複
        var codeExists = await _db.LeaveTypes.AnyAsync(t => t.Code == input.Code);
        if (codeExists)
        {
            ModelState.AddModelError(nameof(input.Code), "此代碼已被使用");
            return View(input);
        }

        var entity = new LeaveType
        {
            Name = input.Name,
            Code = input.Code,
            SortOrder = input.SortOrder,
            Description = input.Description,
            IsActive = input.IsActive
        };
        _db.LeaveTypes.Add(entity);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"假別「{entity.Name}」已建立";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 編輯
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var entity = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return NotFound();

        var vm = new LeaveTypeEditViewModel
        {
            Id = entity.Id,
            Name = entity.Name,
            Code = entity.Code,
            SortOrder = entity.SortOrder,
            Description = entity.Description,
            IsActive = entity.IsActive
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(LeaveTypeEditViewModel input)
    {
        if (!ModelState.IsValid) return View(input);

        var entity = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == input.Id);
        if (entity is null) return NotFound();

        // 代碼不可與其他筆重複
        var codeConflict = await _db.LeaveTypes
            .AnyAsync(t => t.Code == input.Code && t.Id != input.Id);
        if (codeConflict)
        {
            ModelState.AddModelError(nameof(input.Code), "此代碼已被其他假別使用");
            return View(input);
        }

        entity.Name = input.Name;
        entity.Code = input.Code;
        entity.SortOrder = input.SortOrder;
        entity.Description = input.Description;
        entity.IsActive = input.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"假別「{entity.Name}」已更新";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 切換啟用狀態
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var entity = await _db.LeaveTypes.FirstOrDefaultAsync(t => t.Id == id);
        if (entity is null) return NotFound();

        entity.IsActive = !entity.IsActive;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"假別「{entity.Name}」已{(entity.IsActive ? "啟用" : "停用")}";
        return RedirectToAction(nameof(Index));
    }
}
