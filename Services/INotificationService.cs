using LeaveSystem.Models.Entities;

namespace LeaveSystem.Services;

/// <summary>
/// 站內通知服務。負責建立、查詢、標記已讀。
/// </summary>
public interface INotificationService
{
    Task<int> CreateAsync(int userId, string title, string message, string? url);
    Task<IReadOnlyList<Notification>> GetForUserAsync(int userId);
    Task<int> GetUnreadCountAsync(int userId);
    Task<bool> MarkReadAsync(int notificationId, int userId);
    Task<int> MarkAllReadAsync(int userId);
}
