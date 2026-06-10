using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers.Admin;

/// <summary>
/// 行政後台 ─ 使用者管理。
/// 路由：/Admin/Users/...
///
/// 為什麼擺在 Controllers/Admin 子資料夾？
/// 之後行政後台會有不少 Controller（班期、假別、簽核規則…），
/// 集中放一個資料夾比較好整理；命名空間也跟著調整。
///
/// 注意：這裡不使用 ASP.NET Core Area 機制，
/// 「/Admin/Users」只是由下面的 [Route] 屬性產生的路由前綴而已。
/// </summary>
[Route("Admin/Users/{action=Index}/{id?}")]
[Authorize(Roles = "Admin,Staff")]
public class UsersController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public UsersController(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    // ─────────────────────────────────────────────────────────────
    // 列表
    // ─────────────────────────────────────────────────────────────
    public async Task<IActionResult> Index()
    {
        var users = await _db.UserAccounts
            .Include(u => u.Cohort)
            .Include(u => u.UserRoles).ThenInclude(ur => ur.Role)
            .OrderBy(u => u.Username)
            .AsNoTracking()
            .ToListAsync();

        return View(users);
    }

    // ─────────────────────────────────────────────────────────────
    // 建立
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new UserCreateViewModel();
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(UserCreateViewModel input)
    {
        // 步驟 1：基本欄位驗證
        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }

        // 步驟 2：帳號不可重複
        var exists = await _db.UserAccounts.AnyAsync(u => u.Username == input.Username);
        if (exists)
        {
            ModelState.AddModelError(nameof(input.Username), "此帳號已被使用");
            await PopulateOptionsAsync(input);
            return View(input);
        }

        // 步驟 3：將表單轉成 Entity，密碼用 BCrypt 雜湊後再存
        var user = new UserAccount
        {
            Username = input.Username,
            DisplayName = input.DisplayName,
            Email = input.Email,
            PasswordHash = _passwordHasher.Hash(input.Password),
            CohortId = input.CohortId,
            IsActive = input.IsActive,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // 步驟 4：建立角色關聯
        foreach (var roleId in input.RoleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole { RoleId = roleId });
        }

        _db.UserAccounts.Add(user);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"已建立帳號「{user.Username}」";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 編輯
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var user = await _db.UserAccounts
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        var vm = new UserEditViewModel
        {
            Id = user.Id,
            Username = user.Username,
            DisplayName = user.DisplayName,
            Email = user.Email,
            CohortId = user.CohortId,
            IsActive = user.IsActive,
            RoleIds = user.UserRoles.Select(ur => ur.RoleId).ToList()
        };
        await PopulateOptionsAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(int id, UserEditViewModel input)
    {
        if (id != input.Id) return BadRequest();

        if (!ModelState.IsValid)
        {
            await PopulateOptionsAsync(input);
            return View(input);
        }

        var user = await _db.UserAccounts
            .Include(u => u.UserRoles)
            .FirstOrDefaultAsync(u => u.Id == id);

        if (user is null) return NotFound();

        user.DisplayName = input.DisplayName;
        user.Email = input.Email;
        user.CohortId = input.CohortId;
        user.IsActive = input.IsActive;
        user.UpdatedAt = DateTime.UtcNow;

        // 重置角色清單：先清掉舊的，再加新的（最簡潔的更新方式）
        user.UserRoles.Clear();
        foreach (var roleId in input.RoleIds.Distinct())
        {
            user.UserRoles.Add(new UserRole { UserId = user.Id, RoleId = roleId });
        }

        await _db.SaveChangesAsync();

        TempData["Success"] = $"已更新帳號「{user.Username}」";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 重設密碼
    // ─────────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> ResetPassword(int id)
    {
        var user = await _db.UserAccounts.FindAsync(id);
        if (user is null) return NotFound();

        var vm = new ResetPasswordViewModel { UserId = user.Id, Username = user.Username };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(ResetPasswordViewModel input)
    {
        if (!ModelState.IsValid) return View(input);

        var user = await _db.UserAccounts.FindAsync(input.UserId);
        if (user is null) return NotFound();

        user.PasswordHash = _passwordHasher.Hash(input.NewPassword);
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"已重設「{user.Username}」的密碼";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 啟用 / 停用切換
    // ─────────────────────────────────────────────────────────────
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var user = await _db.UserAccounts.FindAsync(id);
        if (user is null) return NotFound();

        user.IsActive = !user.IsActive;
        user.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = $"已將「{user.Username}」設為 {(user.IsActive ? "啟用" : "停用")}";
        return RedirectToAction(nameof(Index));
    }

    // ─────────────────────────────────────────────────────────────
    // 共用：填入下拉選單資料
    // ─────────────────────────────────────────────────────────────
    private async Task PopulateOptionsAsync(UserCreateViewModel vm)
    {
        vm.AvailableRoles = await LoadRolesAsync();
        vm.AvailableCohorts = await LoadCohortsAsync();
    }

    private async Task PopulateOptionsAsync(UserEditViewModel vm)
    {
        vm.AvailableRoles = await LoadRolesAsync();
        vm.AvailableCohorts = await LoadCohortsAsync();
    }

    private async Task<List<RoleOption>> LoadRolesAsync()
    {
        return await _db.Roles
            .OrderBy(r => r.Id)
            .Select(r => new RoleOption { Id = r.Id, Name = r.Name, DisplayName = r.DisplayName })
            .AsNoTracking()
            .ToListAsync();
    }

    private async Task<List<CohortOption>> LoadCohortsAsync()
    {
        return await _db.Cohorts
            .OrderBy(c => c.Name)
            .Select(c => new CohortOption { Id = c.Id, Name = c.Name })
            .AsNoTracking()
            .ToListAsync();
    }
}
