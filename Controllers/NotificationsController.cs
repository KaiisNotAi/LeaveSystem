using System.Security.Claims;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers;

/// <summary>
/// 站內通知中心。
/// 路由：/Notifications/...
/// </summary>
[Route("Notifications/{action=Index}/{id?}")]
[Authorize]
public class NotificationsController : Controller
{
    private readonly INotificationService _service;

    public NotificationsController(INotificationService service)
    {
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var items = await _service.GetForUserAsync(userId.Value);
        var vm = new NotificationIndexViewModel
        {
            Items = items.Select(n => new NotificationItemViewModel
            {
                Id = n.Id,
                Title = n.Title,
                Message = n.Message,
                Url = n.Url,
                IsRead = n.IsRead,
                CreatedAt = n.CreatedAt,
                ReadAt = n.ReadAt
            }).ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRead(int id, string? returnUrl)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        await _service.MarkReadAsync(id, userId.Value);

        if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
        {
            return Redirect(returnUrl);
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkAllRead()
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var affected = await _service.MarkAllReadAsync(userId.Value);
        TempData["Success"] = $"已標記 {affected} 則通知為已讀。";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// 點擊通知：標記已讀後跳轉到 Url。無 Url 則回列表。
    /// </summary>
    [HttpGet]
    public async Task<IActionResult> Go(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null) return Forbid();

        var items = await _service.GetForUserAsync(userId.Value);
        var target = items.FirstOrDefault(x => x.Id == id);
        if (target is null) return NotFound();

        await _service.MarkReadAsync(id, userId.Value);

        if (!string.IsNullOrWhiteSpace(target.Url) && Url.IsLocalUrl(target.Url))
        {
            return Redirect(target.Url);
        }
        return RedirectToAction(nameof(Index));
    }

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }
}
