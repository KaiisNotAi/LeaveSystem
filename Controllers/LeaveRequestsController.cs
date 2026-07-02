using System.Security.Claims;
using LeaveSystem.Data;
using LeaveSystem.Models.Entities;
using LeaveSystem.Models.Enums;
using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Controllers;

/// <summary>
/// 學員請假申請。
/// 路由：/LeaveRequests/...
/// </summary>
[Route("LeaveRequests/{action=Index}/{id?}")]
[Authorize(Roles = "Student")]
public class LeaveRequestsController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILeaveCalculator _leaveCalculator;

    public LeaveRequestsController(AppDbContext db, ILeaveCalculator leaveCalculator)
    {
        _db = db;
        _leaveCalculator = leaveCalculator;
    }

    public async Task<IActionResult> Index()
    {
        var studentId = GetCurrentUserId();
        if (studentId is null)
        {
            return Forbid();
        }

        var items = await _db.LeaveRequests
            .Where(r => r.StudentId == studentId.Value)
            .OrderByDescending(r => r.CreatedAt)
            .Select(r => new LeaveRequestListItemViewModel
            {
                Id = r.Id,
                LeaveTypeName = r.LeaveType.Name,
                StartAt = r.StartAt,
                EndAt = r.EndAt,
                TotalHours = r.TotalHours,
                Status = r.Status,
                CurrentLevel = r.CurrentLevel,
                CreatedAt = r.CreatedAt
            })
            .AsNoTracking()
            .ToListAsync();

        return View(items);
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var vm = new LeaveRequestCreateViewModel();
        await PopulateLeaveTypesAsync(vm);
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(LeaveRequestCreateViewModel input)
    {
        if (!ModelState.IsValid)
        {
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        var studentId = GetCurrentUserId();
        if (studentId is null)
        {
            return Forbid();
        }

        var student = await _db.UserAccounts
            .Include(u => u.Cohort)
            .FirstOrDefaultAsync(u => u.Id == studentId.Value && u.IsActive);
        if (student is null)
        {
            return Forbid();
        }

        if (student.Cohort is null)
        {
            ModelState.AddModelError(string.Empty, "你的帳號尚未指派班期，暫時無法送出請假申請。請聯絡行政人員。");
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        if (!input.StartHour.HasValue || !input.EndHour.HasValue)
        {
            ModelState.AddModelError(string.Empty, "請選擇完整的起訖時段。");
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        var leaveTypeExists = await _db.LeaveTypes
            .AnyAsync(t => t.Id == input.LeaveTypeId && t.IsActive);
        if (!leaveTypeExists)
        {
            ModelState.AddModelError(nameof(input.LeaveTypeId), "選擇的假別不存在或已停用，請重新選擇。");
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        var startAt = input.StartDate.Date.AddHours(input.StartHour.Value);
        var endAt = input.EndDate.Date.AddHours(input.EndHour.Value);

        if (!_leaveCalculator.TryCalculate(startAt, endAt, out var totalHours, out var calcError))
        {
            ModelState.AddModelError(string.Empty, calcError ?? "請假時段計算失敗，請檢查輸入。");
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        // 班期上限檢查：已核准累計 + 本次申請不得超過班期上限。
        var approvedHours = await _db.LeaveRequests
            .Where(r => r.StudentId == student.Id && r.Status == LeaveStatus.Approved)
            .SumAsync(r => (decimal?)r.TotalHours) ?? 0m;

        var wouldBeTotal = approvedHours + totalHours;
        if (wouldBeTotal > student.Cohort.LeaveLimitHours)
        {
            ModelState.AddModelError(
                string.Empty,
                $"送出後將超過班期請假上限 {student.Cohort.LeaveLimitHours} 小時（目前已核准 {approvedHours:0.##} 小時，本次 {totalHours:0.##} 小時）。");
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        // 依請假時數找到對應簽核規則。
        var matchedRule = await _db.ApprovalRules
            .OrderBy(r => r.MinHours)
            .FirstOrDefaultAsync(r =>
                r.MinHours <= totalHours &&
                (!r.MaxHours.HasValue || totalHours <= r.MaxHours.Value));

        if (matchedRule is null)
        {
            ModelState.AddModelError(string.Empty, "找不到符合此請假時數的簽核規則，請聯絡行政人員檢查設定。" );
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        var requiredRoles = ApprovalRoleJson.Deserialize(matchedRule.RequiredRoles);
        if (!requiredRoles.Any())
        {
            ModelState.AddModelError(string.Empty, "簽核規則未設定有效角色，請聯絡行政人員。" );
            await PopulateLeaveTypesAsync(input);
            return View(input);
        }

        var pendingSteps = requiredRoles
            .Select((role, index) => new LeaveRequestStep
            {
                Level = index + 1,
                ApproverRole = role,
                Decision = ApprovalDecision.Pending
            })
            .ToList();

        var now = DateTime.UtcNow;
        var leaveRequest = new LeaveRequest
        {
            StudentId = student.Id,
            LeaveTypeId = input.LeaveTypeId,
            StartAt = startAt,
            EndAt = endAt,
            TotalHours = totalHours,
            Reason = input.Reason.Trim(),
            Status = LeaveStatus.Pending,
            CurrentLevel = 1,
            CreatedAt = now,
            UpdatedAt = now,
            Steps = pendingSteps
        };

        _db.LeaveRequests.Add(leaveRequest);
        await _db.SaveChangesAsync();

        TempData["Success"] = $"請假申請已送出（共 {totalHours:0.##} 小時，{pendingSteps.Count} 關簽核）。";
        return RedirectToAction(nameof(Index));
    }

    private async Task PopulateLeaveTypesAsync(LeaveRequestCreateViewModel vm)
    {
        vm.LeaveTypeOptions = await _db.LeaveTypes
            .Where(t => t.IsActive)
            .OrderBy(t => t.SortOrder)
            .ThenBy(t => t.Name)
            .Select(t => new LeaveTypeOption
            {
                Id = t.Id,
                Name = t.Name
            })
            .AsNoTracking()
            .ToListAsync();
    }

    private int? GetCurrentUserId()
    {
        var idValue = User.FindFirstValue(ClaimTypes.NameIdentifier);
        return int.TryParse(idValue, out var userId) ? userId : null;
    }
}
