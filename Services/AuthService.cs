using System.Security.Claims;
using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Services;

/// <summary>
/// 身分驗證服務實作（Cookie 認證）。
/// </summary>
public class AuthService : IAuthService
{
    private readonly AppDbContext _db;
    private readonly IPasswordHasher _passwordHasher;

    public AuthService(AppDbContext db, IPasswordHasher passwordHasher)
    {
        _db = db;
        _passwordHasher = passwordHasher;
    }

    /// <summary>
    /// 驗證帳號密碼。
    /// </summary>
    public async Task<UserAccount?> ValidateAsync(string username, string password)
    {
        // 步驟 1：查帳號（含 Roles 一起載入，登入後就能直接寫入 Claims）
        var user = await _db.UserAccounts
            .Include(u => u.UserRoles)
                .ThenInclude(ur => ur.Role)
            .FirstOrDefaultAsync(u => u.Username == username);

        if (user is null) return null;

        // 步驟 2：拒絕停用帳號
        if (!user.IsActive) return null;

        // 步驟 3：比對密碼雜湊
        if (!_passwordHasher.Verify(password, user.PasswordHash)) return null;

        return user;
    }

    /// <summary>
    /// 寫入 Cookie 完成登入。
    /// </summary>
    public async Task SignInAsync(HttpContext context, UserAccount user, bool isPersistent = false)
    {
        // 步驟 1：建立此使用者的 Claims（每筆 Claim = 一個身分屬性）
        var claims = new List<Claim>
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.DisplayName),
            new Claim("Username", user.Username)
        };

        // 步驟 2：每個角色加一筆 Claim（[Authorize(Roles="Admin")] 會比對這些）
        foreach (var ur in user.UserRoles)
        {
            if (ur.Role is not null)
            {
                claims.Add(new Claim(ClaimTypes.Role, ur.Role.Name));
            }
        }

        // 步驟 3：包成 ClaimsIdentity 與 ClaimsPrincipal（ASP.NET Core 標準身分容器）
        var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
        var principal = new ClaimsPrincipal(identity);

        // 步驟 4：設定 Cookie 參數
        var properties = new AuthenticationProperties
        {
            IsPersistent = isPersistent,                         // 勾選「記住我」就持久化
            ExpiresUtc = DateTimeOffset.UtcNow.AddHours(8)       // Cookie 有效 8 小時
        };

        // 步驟 5：呼叫 ASP.NET Core 內建簽入方法寫 Cookie
        await context.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            properties);
    }

    /// <summary>
    /// 清除 Cookie 完成登出。
    /// </summary>
    public Task SignOutAsync(HttpContext context)
    {
        return context.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
    }
}
