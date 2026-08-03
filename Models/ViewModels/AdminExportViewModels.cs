using System.ComponentModel.DataAnnotations;
using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.ViewModels;

// ═══════════════════════════════════════════════════════════════
// Phase 8 — 行政匯出報表 ViewModels（/Admin/Export）
//
// 用途：行政 / 系統管理員針對某班期，查詢個別學生或全部學生的
//       「請假明細 + 曠課明細（＋彙總）」並匯出 CSV / Excel。
//
// 授權：Staff / Admin（見 Controllers/Admin/ExportController）。
// ═══════════════════════════════════════════════════════════════

/// <summary>
/// 匯出頁的篩選條件。
/// - <see cref="CohortId"/> 必填（一次只匯一個班期，避免產出過大檔案）。
/// - <see cref="StudentId"/> 未填 = 該班期全部學生；有填 = 僅該學員。
/// - 日期起迄選填，套用邏輯：
///     * 請假明細比對 <c>StartAt</c>（沿用 <see cref="AdminReportQuery"/> 慣例）
///     * 曠課明細比對 <c>OccurredAt</c>（沿用 <see cref="AbsenceRecordAdminQuery"/> 慣例）
/// </summary>
public class AdminExportQuery
{
    [Required(ErrorMessage = "請先選擇班期")]
    [Display(Name = "班期")]
    public int? CohortId { get; set; }

    [Display(Name = "學員（未選＝該班期全部）")]
    public int? StudentId { get; set; }

    /// <summary>
    /// 請假單狀態過濾（僅影響請假明細 sheet 與請假 CSV）：
    ///   ‧ 未選 → 全狀態
    ///   ‧ 有選 → 只匯出該狀態的請假單（曠課明細不受影響）
    /// </summary>
    [Display(Name = "假單狀態")]
    public LeaveStatus? Status { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "日期起")]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "日期迄")]
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// 匯出用的請假單明細列（一張單一列）。
///
/// 刻意包含**全狀態**（Pending / Approved / Rejected / Cancelled…），
/// 讓行政看到誰在進行中、誰被駁回，用 <see cref="Status"/> 欄位識別。
/// 若日後想只匯核准，可在 UI 上加狀態勾選再過濾。
/// </summary>
public class AdminLeaveDetailRow
{
    public int Id { get; set; }
    public DateTime CreatedAt { get; set; }
    public string CohortName { get; set; } = string.Empty;
    public string StudentDisplayName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal TotalHours { get; set; }
    public LeaveStatus Status { get; set; }

    /// <summary>目前簽核關卡（1..N）；僅 Pending 時具參考意義。</summary>
    public int CurrentLevel { get; set; }

    public string Reason { get; set; } = string.Empty;
}

/// <summary>
/// 匯出頁完整資料（表單回填 + 下拉選項）。
/// 匯出 action 本身不用這個 VM，只有 GET Index 頁面用。
/// </summary>
public class AdminExportPageViewModel
{
    public AdminExportQuery Query { get; set; } = new();

    /// <summary>班期下拉。</summary>
    public List<CohortOption> CohortOptions { get; set; } = new();

    /// <summary>
    /// 學員下拉（含 <see cref="UserOption.CohortId"/>），View 端以
    /// <c>data-cohort-id</c> 屬性掛出，讓前端 JS 依所選班期即時過濾。
    /// </summary>
    public List<UserOption> StudentOptions { get; set; } = new();
}
