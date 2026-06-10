using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 行政後台「建立使用者」表單。
/// </summary>
public class UserCreateViewModel
{
    [Required(ErrorMessage = "請輸入帳號")]
    [StringLength(50, MinimumLength = 3, ErrorMessage = "帳號長度需為 3 ~ 50 字")]
    [Display(Name = "帳號")]
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入顯示名稱")]
    [StringLength(50)]
    [Display(Name = "姓名")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress(ErrorMessage = "Email 格式不正確")]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "請輸入初始密碼")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼至少 6 碼")]
    [DataType(DataType.Password)]
    [Display(Name = "初始密碼")]
    public string Password { get; set; } = string.Empty;

    [Required(ErrorMessage = "請至少選擇一個角色")]
    [Display(Name = "角色")]
    public List<int> RoleIds { get; set; } = new();

    [Display(Name = "所屬班期（學員必填）")]
    public int? CohortId { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    // 下拉選單資料來源（由 Controller 填入）
    public List<RoleOption> AvailableRoles { get; set; } = new();
    public List<CohortOption> AvailableCohorts { get; set; } = new();
}

/// <summary>
/// 行政後台「編輯使用者」表單（不含密碼，密碼有獨立 Reset 動作）。
/// </summary>
public class UserEditViewModel
{
    public int Id { get; set; }

    [Display(Name = "帳號")]
    public string Username { get; set; } = string.Empty; // 唯讀顯示

    [Required]
    [StringLength(50)]
    [Display(Name = "姓名")]
    public string DisplayName { get; set; } = string.Empty;

    [EmailAddress]
    [Display(Name = "Email")]
    public string? Email { get; set; }

    [Required(ErrorMessage = "請至少選擇一個角色")]
    [Display(Name = "角色")]
    public List<int> RoleIds { get; set; } = new();

    [Display(Name = "所屬班期")]
    public int? CohortId { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;

    public List<RoleOption> AvailableRoles { get; set; } = new();
    public List<CohortOption> AvailableCohorts { get; set; } = new();
}

public class RoleOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class CohortOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

/// <summary>
/// 重設密碼用。
/// </summary>
public class ResetPasswordViewModel
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入新密碼")]
    [StringLength(100, MinimumLength = 6, ErrorMessage = "密碼至少 6 碼")]
    [DataType(DataType.Password)]
    [Display(Name = "新密碼")]
    public string NewPassword { get; set; } = string.Empty;
}
