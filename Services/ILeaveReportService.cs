using LeaveSystem.Models.ViewModels;

namespace LeaveSystem.Services;

/// <summary>
/// 紀錄查詢與時數統計服務介面（Phase 5）。
///
/// 業務規則見 <c>docs/04-請假與簽核流程.md §7</c>：
///   ‧ 學員查詢：以自己 UserId 為主 WHERE 條件（Controller 從 Cookie 取，Service 不越權）
///   ‧ 累計時數：只算 <c>LeaveStatus.Approved</c>（與班期上限口徑一致）
///   ‧ 行政報表：<c>Cohort × Student × LeaveType × YearMonth</c> 分組，
///     跨月請假整筆算開始月
///
/// 設計原則：
///   ‧ 純業務邏輯，不依賴 HttpContext；所有輸入透過參數傳入，方便單元測試。
///   ‧ 回傳「該畫面所需的完整資料」（含下拉選項），Controller 一行呼叫即可
///     ─ 與既有 <see cref="IApprovalService"/> 體例一致。
/// </summary>
public interface ILeaveReportService
{
    /// <summary>
    /// 依篩選條件查詢指定學員的請假紀錄。
    ///
    /// 篩選欄位（皆選填）：
    ///   ‧ <c>query.Status</c>       ─ 請假單狀態
    ///   ‧ <c>query.LeaveTypeId</c>  ─ 假別
    ///   ‧ <c>query.DateFrom</c>     ─ 起始日期（含），對應 <c>r.StartAt &gt;= DateFrom</c>
    ///   ‧ <c>query.DateTo</c>       ─ 結束日期（含），對應 <c>r.StartAt &lt;= DateTo</c>
    ///
    /// 排序：<c>CreatedAt DESC</c>（最新在最上面）。
    ///
    /// 安全性：
    ///   ‧ <paramref name="studentId"/> 一律作為 WHERE 主條件，
    ///     學員即便偽造 query 內容也無法讀到他人資料。
    ///   ‧ Controller 應以「當前登入者的 UserId」呼叫本方法。
    /// </summary>
    /// <param name="studentId">學員 UserId（Controller 從 Cookie 取）。</param>
    /// <param name="query">篩選條件（皆選填）。</param>
    Task<IReadOnlyList<MyLeaveRequestListItem>> GetMyRequestsAsync(int studentId, MyLeaveRequestQuery query);

    /// <summary>
    /// 取得指定學員的「各假別累計時數」+「班期上限」資訊。
    ///
    /// 統計口徑：**只累計 <c>LeaveStatus.Approved</c> 的請假單**
    /// （<c>Pending</c> / <c>Rejected</c> / <c>Cancelled</c> 不計入）。
    ///
    /// 若學員尚未指派班期（<c>CohortId == null</c>），
    /// 回傳的 <see cref="MyLeaveSummaryViewModel.CohortLeaveLimitHours"/> 與
    /// <see cref="MyLeaveSummaryViewModel.CohortName"/> 為 <c>null</c>，
    /// View 應據此隱藏上限進度條並提示。
    /// </summary>
    /// <param name="studentId">學員 UserId。</param>
    Task<MyLeaveSummaryViewModel> GetMyLeaveTypeSummaryAsync(int studentId);

    /// <summary>
    /// 取得跨學員的彙總報表資料（僅行政使用）。
    ///
    /// 分組維度：<c>Cohort × Student × LeaveType × YearMonth</c>；
    /// 統計口徑：**只算 <c>LeaveStatus.Approved</c>**；
    /// 月份切法：依 <c>StartAt.Year</c> / <c>StartAt.Month</c>，
    /// 跨月請假整筆算開始月（見 <c>docs/04 §7.3</c>）。
    ///
    /// 回傳的 <see cref="AdminReportViewModel"/> 已包含：
    ///   ‧ <c>Query</c>：原輸入條件（供 Razor 表單回填）
    ///   ‧ <c>Rows</c>：彙總結果（依 Cohort → Student → LeaveType → Year → Month 排序）
    ///   ‧ <c>CohortOptions</c> / <c>StudentOptions</c> / <c>LeaveTypeOptions</c>：篩選下拉
    ///
    /// Controller 只需將整個 ViewModel 交給 View 即可，不必再另查資料庫。
    /// </summary>
    /// <param name="query">篩選條件（皆選填）。</param>
    Task<AdminReportViewModel> GetAdminReportAsync(AdminReportQuery query);

    /// <summary>
    /// Phase 8：取得指定班期（可再限定單一學員）在時間區間內的請假明細（全狀態），
    /// 供 <c>/Admin/Export</c> 匯出頁面使用。
    ///
    /// 資料範圍：
    ///   ‧ 主 WHERE：學員所屬 <c>CohortId</c> = <c>query.CohortId</c>（必填）
    ///   ‧ 選填：<c>query.StudentId</c>、<c>query.DateFrom</c>、<c>query.DateTo</c>
    ///   ‧ **包含所有狀態**（Pending / Approved / Rejected …），
    ///     讓行政能一次看清「已核准 / 進行中 / 被駁回」。
    ///
    /// 排序：<c>StartAt DESC</c>。
    ///
    /// 日期邊界：<c>DateTo</c> 若時間為 00:00:00，Service 會補到當日 23:59:59.9999
    /// （體例對齊 <see cref="GetAdminReportAsync"/>）。
    /// </summary>
    Task<IReadOnlyList<AdminLeaveDetailRow>> GetLeaveDetailsForAdminAsync(AdminExportQuery query);
}
