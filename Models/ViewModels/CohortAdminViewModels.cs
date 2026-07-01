using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 行政後台「建立班期」表單。
/// </summary>
public class CohortCreateViewModel
{
    [Required(ErrorMessage = "請輸入班期名稱")]
    [StringLength(100, ErrorMessage = "班期名稱最長 100 字")]
    [Display(Name = "班期名稱")]
    public string Name { get; set; } = string.Empty;

    [Required(ErrorMessage = "請選擇開課日期")]
    [DataType(DataType.Date)]
    [Display(Name = "開課日期")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "請選擇結訓日期")]
    [DataType(DataType.Date)]
    [Display(Name = "結訓日期")]
    public DateTime EndDate { get; set; } = DateTime.Today.AddMonths(6);

    [Required(ErrorMessage = "請輸入總上課時數")]
    [Range(1, 10000, ErrorMessage = "總上課時數必須介於 1 ~ 10000")]
    [Display(Name = "總上課時數")]
    public int TotalHours { get; set; } = 900;

    [Range(0, 100, ErrorMessage = "百分比必須介於 0 ~ 100")]
    [Display(Name = "請假上限百分比（%）")]
    public int LeaveLimitPercent { get; set; } = 10;

    [Display(Name = "導師")]
    public int? TutorUserId { get; set; }

    [Display(Name = "科長")]
    public int? SectionChiefUserId { get; set; }

    [Display(Name = "分署長")]
    public int? BranchDirectorUserId { get; set; }

    // 下拉選單資料來源（由 Controller 依角色填入）
    public List<UserOption> TutorOptions { get; set; } = new();
    public List<UserOption> SectionChiefOptions { get; set; } = new();
    public List<UserOption> BranchDirectorOptions { get; set; } = new();
}

/// <summary>
/// 行政後台「編輯班期」表單。
/// </summary>
public class CohortEditViewModel
{
    public int Id { get; set; }

    [Required]
    [StringLength(100)]
    [Display(Name = "班期名稱")]
    public string Name { get; set; } = string.Empty;

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "開課日期")]
    public DateTime StartDate { get; set; }

    [Required]
    [DataType(DataType.Date)]
    [Display(Name = "結訓日期")]
    public DateTime EndDate { get; set; }

    [Range(1, 10000)]
    [Display(Name = "總上課時數")]
    public int TotalHours { get; set; }

    [Range(0, 100)]
    [Display(Name = "請假上限百分比（%）")]
    public int LeaveLimitPercent { get; set; }

    [Display(Name = "導師")]
    public int? TutorUserId { get; set; }

    [Display(Name = "科長")]
    public int? SectionChiefUserId { get; set; }

    [Display(Name = "分署長")]
    public int? BranchDirectorUserId { get; set; }

    public List<UserOption> TutorOptions { get; set; } = new();
    public List<UserOption> SectionChiefOptions { get; set; } = new();
    public List<UserOption> BranchDirectorOptions { get; set; } = new();
}

/// <summary>
/// 使用者下拉選項（給 Cohort 挑選導師/科長/分署長用）。
/// </summary>
public class UserOption
{
    public int Id { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
}
