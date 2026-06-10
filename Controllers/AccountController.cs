using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers;

/// <summary>
/// 帳號相關 Action：登入、登出、拒絕存取頁面。
/// </summary>
[AllowAnonymous] // 此 Controller 預設不需要登入即可存取（否則登入頁會打不開）
public class AccountController : Controller
{
    private readonly IAuthService _authService;

    public AccountController(IAuthService authService)
    {
        _authService = authService;
    }

    /// <summary>
    /// GET /Account/Login
    /// 顯示登入表單。
    /// </summary>
    [HttpGet]
    public IActionResult Login(string? returnUrl = null)
    {
        // 如果使用者已經登入了，就不必再讓它登入一次
        if (User?.Identity?.IsAuthenticated == true)
        {
            return RedirectToAction("Index", "Home");
        }

        var vm = new LoginViewModel { ReturnUrl = returnUrl };
        return View(vm);
    }

    /// <summary>
    /// POST /Account/Login
    /// 處理登入表單送出。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken] // 防止 CSRF 攻擊
    public async Task<IActionResult> Login(LoginViewModel input)
    {
        // 步驟 1：先檢查表單欄位本身有沒有缺漏（[Required] 等）
        if (!ModelState.IsValid)
        {
            return View(input);
        }

        // 步驟 2：交給 AuthService 驗證帳密 + 載入角色
        var user = await _authService.ValidateAsync(input.Username, input.Password);
        if (user is null)
        {
            // 故意不告知是「帳號錯」還是「密碼錯」，避免讓攻擊者列舉帳號
            ModelState.AddModelError(string.Empty, "帳號或密碼錯誤，或帳號已停用");
            return View(input);
        }

        // 步驟 3：寫入 Cookie 完成登入
        await _authService.SignInAsync(HttpContext, user, input.RememberMe);

        // 步驟 4：導回原本要去的頁面（如果有）
        if (!string.IsNullOrEmpty(input.ReturnUrl) && Url.IsLocalUrl(input.ReturnUrl))
        {
            return Redirect(input.ReturnUrl);
        }

        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// POST /Account/Logout
    /// 登出（用 POST 是為了防止有人靠網址列就能讓別人登出）。
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize]
    public async Task<IActionResult> Logout()
    {
        await _authService.SignOutAsync(HttpContext);
        return RedirectToAction("Index", "Home");
    }

    /// <summary>
    /// GET /Account/AccessDenied
    /// 已登入但角色不足時導向這裡。
    /// </summary>
    [HttpGet]
    public IActionResult AccessDenied()
    {
        return View();
    }
}
