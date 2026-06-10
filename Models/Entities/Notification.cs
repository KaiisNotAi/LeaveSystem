using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 站內通知實體（Phase 7 才會啟用）。
/// 一筆紀錄對應一位接收者；若同一事件要通知多人，會建立多筆。
/// </summary>
public class Notification
{
    public int Id { get; set; }

    /// <summary>
    /// 接收者 UserId。
    /// </summary>
    [Required]
    public int UserId { get; set; }

    /// <summary>
    /// 通知標題（如「您有新的請假申請待簽核」）。
    /// </summary>
    [Required]
    [StringLength(100)]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// 通知內容。
    /// </summary>
    [Required]
    [StringLength(500)]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// 點擊通知後跳轉的網址（如請假單詳情頁）。可為空。
    /// </summary>
    [StringLength(500)]
    public string? Url { get; set; }

    /// <summary>
    /// 是否已讀。預設未讀。
    /// </summary>
    public bool IsRead { get; set; } = false;

    /// <summary>
    /// 建立時間。
    /// </summary>
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// 已讀時間。IsRead=true 時才有值。
    /// </summary>
    public DateTime? ReadAt { get; set; }

    // ─── 導覽屬性 ───

    /// <summary>
    /// 接收者。
    /// </summary>
    public UserAccount User { get; set; } = null!;
}
