using System.Security.Claims;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LeaveSystem.Controllers;

/// <summary>
/// 簽核人工作台。
/// 路由：/Approvals/...
/// </summary>
[Route("Approvals/{action=Index}/{id?}")]
[Authorize(Roles = "Tutor,SectionChief,BranchDirector")]
public class ApprovalsController : Controller
{
    private readonly IApprovalService _approvalService;

    public ApprovalsController(IApprovalService approvalService)
    {
        _approvalService = approvalService;
    }

    public async Task<IActionResult> Index()
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        var vm = new ApprovalIndexViewModel
        {
            PendingItems = (await _approvalService.GetPendingForUserAsync(userId.Value)).ToList(),
            HistoryItems = (await _approvalService.GetHistoryForUserAsync(userId.Value)).ToList()
        };

        return View(vm);
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        var detail = await _approvalService.GetDetailForUserAsync(id, userId.Value);
        if (detail is null)
        {
            // 若不是此單可見對象（或單據不存在）一律回 404，避免外洩資訊。
            return NotFound();
        }

        return View(detail);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(ApprovalActionInputModel input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        var result = await _approvalService.ApproveAsync(input.StepId, userId.Value, input.Comment);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "核准失敗，請稍後再試。";
            return await RedirectToDetailsByStepIdAsync(input.StepId);
        }

        TempData["Success"] = result.Outcome switch
        {
            ApprovalOutcome.AdvancedToNextLevel => "簽核成功，已推進到下一關。",
            ApprovalOutcome.FullyApproved => "簽核成功，整張請假單已核准。",
            _ => "簽核成功。"
        };

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(ApprovalActionInputModel input)
    {
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return Forbid();
        }

        var result = await _approvalService.RejectAsync(input.StepId, userId.Value, input.Comment ?? string.Empty);
        if (!result.Success)
        {
            TempData["Error"] = result.ErrorMessage ?? "駁回失敗，請稍後再試。";
            return await RedirectToDetailsByStepIdAsync(input.StepId);
        }

        TempData["Success"] = "已駁回此請假申請。";
        return RedirectToAction(nameof(Index));
    }

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }

    private async Task<IActionResult> RedirectToDetailsByStepIdAsync(int stepId)
    {
        // 發生錯誤時回到原單據詳細頁，讓使用者能立即修正再送一次。
        // 這裡重用服務查詢，避免 Controller 直接碰 DbContext。
        var userId = GetCurrentUserId();
        if (userId is null)
        {
            return RedirectToAction(nameof(Index));
        }

        var pending = await _approvalService.GetPendingForUserAsync(userId.Value);
        var matched = pending.FirstOrDefault(p => p.StepId == stepId);
        if (matched is not null)
        {
            return RedirectToAction(nameof(Details), new { id = matched.LeaveRequestId });
        }

        var history = await _approvalService.GetHistoryForUserAsync(userId.Value);
        var historyMatched = history.FirstOrDefault(h => h.StepId == stepId);
        if (historyMatched is not null)
        {
            return RedirectToAction(nameof(Details), new { id = historyMatched.LeaveRequestId });
        }

        return RedirectToAction(nameof(Index));
    }
}
