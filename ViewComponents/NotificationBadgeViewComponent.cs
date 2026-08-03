using System.Security.Claims;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.ViewComponents;

/// <summary>
/// Layout 右上角的通知徽章。未登入顯示 0。
/// </summary>
public class NotificationBadgeViewComponent : ViewComponent
{
    private readonly INotificationService _service;

    public NotificationBadgeViewComponent(INotificationService service)
    {
        _service = service;
    }

    public async Task<IViewComponentResult> InvokeAsync()
    {
        var idValue = (HttpContext.User?.FindFirstValue(ClaimTypes.NameIdentifier));
        if (!int.TryParse(idValue, out var userId))
        {
            return View(0);
        }
        var count = await _service.GetUnreadCountAsync(userId);
        return View(count);
    }
}
