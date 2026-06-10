using LeaveSystem.Models.Entities;

namespace LeaveSystem.Services;

/// <summary>
/// 身分驗證服務介面。
/// 集中處理「驗證帳密、簽入 Cookie、登出」三件事，
/// AccountController 不直接碰 DbContext 與 ClaimsPrincipal。
/// </summary>
public interface IAuthService
{
    /// <summary>
    /// 驗證帳號密碼。成功回傳 UserAccount（含 Roles），失敗回傳 null。
    /// </summary>
    Task<UserAccount?> ValidateAsync(string username, string password);

    /// <summary>
    /// 將使用者寫入 Cookie 完成登入。
    /// 會把 UserId、DisplayName、各 Role 都放進 ClaimsPrincipal。
    /// </summary>
    Task SignInAsync(HttpContext context, UserAccount user, bool isPersistent = false);

    /// <summary>
    /// 清除 Cookie 完成登出。
    /// </summary>
    Task SignOutAsync(HttpContext context);
}
