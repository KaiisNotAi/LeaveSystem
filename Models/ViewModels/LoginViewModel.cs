using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 登入頁面的表單資料模型。
///
/// 為什麼用 ViewModel 而不直接用 UserAccount？
/// Entity 帶有資料庫所有欄位（PasswordHash、Id、各種關聯）；
/// 表單只需要使用者輸入的欄位。把它們拆開可以避免「過度繫結（over-posting）」
/// 的安全問題（駭客提交多餘欄位竄改資料）。
/// </summary>
public class LoginViewModel
{
    [Required(ErrorMessage = "請輸入帳號")]
    [Display(Name = "帳號")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入密碼")]
    [DataType(DataType.Password)]
    [Display(Name = "密碼")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "記住我")]
    public bool RememberMe { get; set; }

    /// <summary>
    /// 未登入造訪某頁面被導到登入頁時，原始網址會放在這裡，
    /// 登入成功後會自動跳回去。
    /// </summary>
    public string? ReturnUrl { get; set; }
}
