using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 行政後台「建立假別」表單。
/// </summary>
public class LeaveTypeCreateViewModel
{
    [Required(ErrorMessage = "請輸入假別名稱")]
    [StringLength(50)]
    [Display(Name = "假別名稱")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "請輸入假別代碼")]
    [StringLength(20)]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "代碼僅可使用大寫英文、數字、底線")]
    [Display(Name = "假別代碼")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "顯示排序（數字越小越前面）")]
    public int SortOrder { get; set; } = 0;

    [StringLength(500)]
    [Display(Name = "描述")]
    public string? Description { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; } = true;
}

/// <summary>
/// 行政後台「編輯假別」表單。
/// </summary>
public class LeaveTypeEditViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(50)]
    [Display(Name = "假別名稱")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [StringLength(20)]
    [RegularExpression(@"^[A-Z0-9_]+$", ErrorMessage = "代碼僅可使用大寫英文、數字、底線")]
    [Display(Name = "假別代碼")]
    public string Code { get; set; } = string.Empty;

    [Display(Name = "顯示排序")]
    public int SortOrder { get; set; }

    [StringLength(500)]
    [Display(Name = "描述")]
    public string? Description { get; set; }

    [Display(Name = "啟用")]
    public bool IsActive { get; set; }
}
