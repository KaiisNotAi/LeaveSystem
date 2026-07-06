using System.ComponentModel.DataAnnotations;
using LeaveSystem.Models.Enums;

namespace LeaveSystem.Models.ViewModels;

// ═══════════════════════════════════════════════════════════════
// Phase 5 — 紀錄查詢與時數統計 ViewModels
//
// 一個檔集中放本 Phase 的所有 ViewModel，方便查找與維護
// （與 ApprovalViewModels.cs、LeaveRequestStudentViewModels.cs 同體例）。
//
// 下拉選項刻意不在此檔重新定義，直接重用既有的：
//   - LeaveTypeOption：LeaveRequestStudentViewModels.cs
//   - CohortOption   ：UserAdminViewModels.cs
//   - UserOption     ：CohortAdminViewModels.cs
// ═══════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────
// 1. 學員紀錄查詢（/Reports/Index）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 學員紀錄查詢的篩選輸入模型。
///
/// 所有欄位皆為選填（Nullable）：
///   - 未填 → 該條件不套用（相當於「不限」）
///   - 有填 → Service 層以「AND」串接對應 LINQ 條件
///
/// 篩選規則見 <c>docs/04-請假與簽核流程.md §7.1</c>。
/// </summary>
public class MyLeaveRequestQuery
{
    /// <summary>
    /// 依請假單狀態篩選（如：只看已核准的）。
    /// </summary>
    [Display(Name = "狀態")]
    public LeaveStatus? Status { get; set; }

    /// <summary>
    /// 依假別篩選。
    /// </summary>
    [Display(Name = "假別")]
    public int? LeaveTypeId { get; set; }

    /// <summary>
    /// 起始日期（含）。對應條件：<c>r.StartAt &gt;= DateFrom</c>。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期起")]
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// 結束日期（含）。對應條件：<c>r.StartAt &lt;= DateTo</c>。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期迄")]
    public DateTime? DateTo { get; set; }

    /// <summary>
    /// 假別下拉選項（由 Controller 填入，方便 Razor 表單直接綁定）。
    /// </summary>
    public List<LeaveTypeOption> LeaveTypeOptions { get; set; } = new();
}

/// <summary>
/// 學員紀錄查詢結果的單列資料。
///
/// 與 Phase 3 的 <see cref="LeaveRequestListItemViewModel"/> 相比，
/// Phase 5 額外提供 <see cref="Reason"/> 與 <see cref="RejectReason"/>
/// 讓學員在查詢頁能直接看到申請原因與駁回原因，減少反覆點開詳細頁的困擾。
///
/// 刻意獨立一個型別以隔離 Phase 3 / Phase 5 的演進路徑
/// （日後任一邊要加欄位，不會波及另一邊）。
/// </summary>
public class MyLeaveRequestListItem
{
    public int Id { get; set; }
    public string LeaveTypeName { get; set; } = string.Empty;
    public DateTime StartAt { get; set; }
    public DateTime EndAt { get; set; }
    public decimal TotalHours { get; set; }
    public LeaveStatus Status { get; set; }
    public int CurrentLevel { get; set; }
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// 請假原因（來自 <c>LeaveRequest.Reason</c>，最多 500 字）。
    /// View 可視版面自行截斷顯示，並以 tooltip 或彈窗展開全文。
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// 駁回原因（來自被駁回關卡的 <c>LeaveRequestStep.Comment</c>）。
    ///
    /// 資料來源規則：
    ///   - 只有 <see cref="Status"/> == <see cref="LeaveStatus.Rejected"/> 時才會有值
    ///   - 其他狀態一律為 <c>null</c>
    ///
    /// 依 Phase 4 的簽核邏輯「任一級駁回整張單終止」，
    /// 因此最多只有一筆 <c>Decision=Rejected</c> 的 Step，
    /// Service 層以 <c>FirstOrDefault()</c> 取該筆的 <c>Comment</c>。
    /// </summary>
    public string? RejectReason { get; set; }
}

// ─────────────────────────────────────────────────────────────
// 2. 學員累計時數統計（/Reports/Summary）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 學員 Summary 頁「各假別一列」的統計項目。
///
/// 統計口徑：<c>LeaveStatus.Approved</c>（見 <c>docs/04 §7.2</c>）。
/// </summary>
public class MyLeaveTypeSummaryItem
{
    /// <summary>假別名稱（顯示用）。</summary>
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>此假別已核准時數合計。</summary>
    public decimal TotalHours { get; set; }

    /// <summary>此假別已核准請假單數。</summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// 學員 Summary 頁的完整資料。
///
/// 除各假別明細外，還帶班期上限資訊，讓 View 可繪製進度條
/// （見 <c>docs/04 §7.2</c>）。若學員尚未指派班期，
/// <see cref="CohortLeaveLimitHours"/> 與 <see cref="CohortName"/> 為 <c>null</c>，
/// View 隱藏進度條並提示「尚未指派班期」。
/// </summary>
public class MyLeaveSummaryViewModel
{
    /// <summary>各假別累計明細（依假別排序後填入）。</summary>
    public List<MyLeaveTypeSummaryItem> Items { get; set; } = new();

    /// <summary>跨假別總計時數（已核准）。</summary>
    public decimal TotalApprovedHours { get; set; }

    /// <summary>班期請假上限（小時）。學員無班期時為 null。</summary>
    public int? CohortLeaveLimitHours { get; set; }

    /// <summary>班期名稱。學員無班期時為 null。</summary>
    public string? CohortName { get; set; }
}

// ─────────────────────────────────────────────────────────────
// 3. 行政彙總報表（/Reports/Admin）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 行政彙總報表的篩選輸入模型。
///
/// 皆選填；未填即代表「該維度不限」。
/// 日期起迄採 <see cref="DateTime"/>? 讓 View 直接使用 <c>&lt;input type="date"&gt;</c>
/// 的日期選擇器；<see cref="DateTo"/> 若只帶到日期，Service 會補到當日 <c>23:59:59</c>
/// 以「含當天」為語意（見 <c>docs/04 §7.3</c>）。
///
/// 注意：**篩選粒度到天，但彙總維度仍然是「年月」**（跨月整筆算開始月）。
/// </summary>
public class AdminReportQuery
{
    /// <summary>依班期篩選。</summary>
    [Display(Name = "班期")]
    public int? CohortId { get; set; }

    /// <summary>依學員篩選。</summary>
    [Display(Name = "學員")]
    public int? StudentId { get; set; }

    /// <summary>依假別篩選。</summary>
    [Display(Name = "假別")]
    public int? LeaveTypeId { get; set; }

    /// <summary>
    /// 起始日期（含）。對應條件：<c>r.StartAt &gt;= DateFrom</c>。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期起")]
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// 結束日期（含）。時間若為 <c>00:00:00</c>，Service 會補到當日 <c>23:59:59</c>。
    /// 對應條件：<c>r.StartAt &lt;= DateTo</c>（Service 補時間後）。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期迄")]
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// 行政彙總報表的單列資料。
///
/// 每列 = 一組「班期 × 學員 × 假別 × 年 × 月」的彙總。
/// 月份切法：依 <c>StartAt.Year/Month</c>，跨月請假整筆算開始月（見 <c>docs/04 §7.3</c>）。
/// </summary>
public class AdminReportRow
{
    public string CohortName { get; set; } = string.Empty;
    public string StudentDisplayName { get; set; } = string.Empty;
    public string LeaveTypeName { get; set; } = string.Empty;

    /// <summary>年份（如 2026）。分開存讓 View 顯示彈性更大。</summary>
    public int Year { get; set; }

    /// <summary>月份（1~12）。</summary>
    public int Month { get; set; }

    /// <summary>該分組已核准時數合計。</summary>
    public decimal TotalHours { get; set; }

    /// <summary>該分組已核准請假單數。</summary>
    public int RequestCount { get; set; }
}

/// <summary>
/// 行政彙總報表頁的完整資料。
///
/// 同時帶入：
///   - <see cref="Query"/>：讓表單保留使用者的篩選條件（回填）
///   - <see cref="Rows"/>：彙總結果
///   - 三個下拉選項清單：讓 Razor 直接綁定，不必再另打 API
/// </summary>
public class AdminReportViewModel
{
    /// <summary>使用者輸入的篩選條件（用於表單回填）。</summary>
    public AdminReportQuery Query { get; set; } = new();

    /// <summary>彙總結果列。</summary>
    public List<AdminReportRow> Rows { get; set; } = new();

    // ─── 下拉選項（Controller 填入） ───
    public List<CohortOption> CohortOptions { get; set; } = new();
    public List<UserOption> StudentOptions { get; set; } = new();
    public List<LeaveTypeOption> LeaveTypeOptions { get; set; } = new();
}
