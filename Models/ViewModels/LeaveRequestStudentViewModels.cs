using System.ComponentModel.DataAnnotations;
using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 學員送出請假申請表單。
///
/// 設計說明：
/// - 日期與時段（小時）分開綁定，讓 Razor 下拉選單比較直覺。
/// - 實際寫入資料庫前，再由 Controller 組合成 StartAt / EndAt。
/// </summary>
public class LeaveRequestCreateViewModel
{
    [Required(ErrorMessage = "請選擇假別")]
    [Display(Name = "假別")]
    public int LeaveTypeId { get; set; }

    [Required(ErrorMessage = "請選擇開始日期")]
    [DataType(DataType.Date)]
    [Display(Name = "開始日期")]
    public DateTime StartDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "請選擇開始時間")]
    [Display(Name = "開始時間")]
    public int? StartHour { get; set; }

    [Required(ErrorMessage = "請選擇結束日期")]
    [DataType(DataType.Date)]
    [Display(Name = "結束日期")]
    public DateTime EndDate { get; set; } = DateTime.Today;

    [Required(ErrorMessage = "請選擇結束時間")]
    [Display(Name = "結束時間")]
    public int? EndHour { get; set; }

    [Required(ErrorMessage = "請輸入請假原因")]
    [StringLength(500, ErrorMessage = "請假原因最長 500 字")]
    [Display(Name = "請假原因")]
    public string Reason { get; set; } = string.Empty;

    public List<LeaveTypeOption> LeaveTypeOptions { get; set; } = new();
    public List<HourOption> StartHourOptions { get; set; } = HourOptionCatalog.StartHours;
    public List<HourOption> EndHourOptions { get; set; } = HourOptionCatalog.EndHours;
}

/// <summary>
/// 學員查看「我的請假申請」列表列資料。
/// </summary>
public class LeaveRequestListItemViewModel
{
    public int Id { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal TotalHours { get; set; }
    public LeaveStatus Status { get; set; }
    public int CurrentLevel { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class LeaveTypeOption
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class HourOption
{
    public int Hour { get; set; }
    public string Label { get; set; } = string.Empty;
}

/// <summary>
/// 請假表單的整點時段選項。
/// </summary>
public static class HourOptionCatalog
{
    // 開始時間不含 12（午休起點）與 17（當日最後邊界，不能當起點）
    public static List<HourOption> StartHours =>
    [
        new() { Hour = 8, Label = "08:00" },
        new() { Hour = 9, Label = "09:00" },
        new() { Hour = 10, Label = "10:00" },
        new() { Hour = 11, Label = "11:00" },
        new() { Hour = 13, Label = "13:00" },
        new() { Hour = 14, Label = "14:00" },
        new() { Hour = 15, Label = "15:00" },
        new() { Hour = 16, Label = "16:00" }
    ];

    // 結束時間不含 13（午休終點，避免 12~13 無效區間）
    public static List<HourOption> EndHours =>
    [
        new() { Hour = 9, Label = "09:00" },
        new() { Hour = 10, Label = "10:00" },
        new() { Hour = 11, Label = "11:00" },
        new() { Hour = 12, Label = "12:00" },
        new() { Hour = 14, Label = "14:00" },
        new() { Hour = 15, Label = "15:00" },
        new() { Hour = 16, Label = "16:00" },
        new() { Hour = 17, Label = "17:00" }
    ];
}
