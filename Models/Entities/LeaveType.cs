using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 假別實體。
/// 由行政人員後台維護（病假/事假/喪假/公假…）。
/// 不採 enum 是因為機構可能會新增「生理假」「家庭照顧假」等假別，需要動態擴充。
/// </summary>
public class LeaveType
{
    public int Id { get; set; }

    /// <summary>
    /// 假別中文名稱（如「病假」「事假」）。
    /// </summary>
    [Required(ErrorMessage = "假別名稱為必填")]
    [StringLength(50)]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 假別代碼（英數，如「SICK」「PERSONAL」），未來與外部系統整合時使用。
    /// </summary>
    [Required(ErrorMessage = "假別代碼為必填")]
    [StringLength(20)]
    public string Code { get; set; } = string.Empty;

    /// <summary>
    /// 是否啟用。停用後不會出現在學員的請假申請下拉選單中，但歷史資料仍保留。
    /// </summary>
    public bool IsActive { get; set; } = true;

    /// <summary>
    /// 顯示排序（數值越小越前面）。
    /// </summary>
    public int SortOrder { get; set; }

    /// <summary>
    /// 描述（如備註此假別需要附證明等）。
    /// </summary>
    [StringLength(500)]
    public string? Description { get; set; }
}
