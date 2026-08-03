namespace LeaveSystem.Models.ViewModels;

public class NotificationIndexViewModel
{
    public List<NotificationItemViewModel> Items { get; set; } = new();
    public int UnreadCount => Items.Count(i => !i.IsRead);
}

public class NotificationItemViewModel
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
    public string? Url { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? ReadAt { get; set; }
}
