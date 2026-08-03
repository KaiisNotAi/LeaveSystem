using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Services;

/// <inheritdoc />
public class NotificationService : INotificationService
{
    private readonly AppDbContext _db;

    public NotificationService(AppDbContext db)
    {
        _db = db;
    }

    public async Task<int> CreateAsync(int userId, string title, string message, string? url)
    {
        var n = new Notification
        {
            UserId = userId,
            Title = title,
            Message = message,
            Url = url,
            IsRead = false,
            CreatedAt = DateTime.UtcNow
        };
        _db.Notifications.Add(n);
        await _db.SaveChangesAsync();
        return n.Id;
    }

    public async Task<IReadOnlyList<Notification>> GetForUserAsync(int userId)
    {
        return await _db.Notifications
            .Where(n => n.UserId == userId)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .AsNoTracking()
            .ToListAsync();
    }

    public Task<int> GetUnreadCountAsync(int userId)
    {
        return _db.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead);
    }

    public async Task<bool> MarkReadAsync(int notificationId, int userId)
    {
        var n = await _db.Notifications
            .FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
        if (n is null || n.IsRead)
        {
            return n is not null; // 找到但已讀也視為成功；找不到才是 false
        }
        n.IsRead = true;
        n.ReadAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return true;
    }

    public async Task<int> MarkAllReadAsync(int userId)
    {
        var list = await _db.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ToListAsync();
        var now = DateTime.UtcNow;
        foreach (var n in list)
        {
            n.IsRead = true;
            n.ReadAt = now;
        }
        if (list.Count > 0)
        {
            await _db.SaveChangesAsync();
        }
        return list.Count;
    }
}
