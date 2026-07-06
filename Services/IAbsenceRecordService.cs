using LeaveSystem.Models.ViewModels;

namespace LeaveSystem.Services;

/// <summary>
/// 曠課紀錄服務介面（Phase 6）。
///
/// 業務規則見 <c>docs/01-需求說明.md §3.5 / §4</c> 與
/// <c>Models/Entities/AbsenceRecord.cs</c> 類別註釋：
///   ‧ 「行政人員可登錄曠課時數（學員無法自填）」
///   ‧ 只有 Admin / Staff 可以新增、修改、刪除；學員只能查閱自己的紀錄
///   ‧ 曠課紀錄與請假單分開（不掛 <c>LeaveRequestId</c> FK）
///
/// 設計原則（與 <see cref="IApprovalService"/> / <see cref="ILeaveReportService"/> 一致）：
///   ‧ 純業務邏輯，不依賴 HttpContext；所有輸入透過參數傳入，方便單元測試
///   ‧ 回傳「該畫面所需的完整資料」（含下拉選項），Controller 一行呼叫即可
///   ‧ 授權（哪些角色能呼叫哪些方法）由 Controller 的 <c>[Authorize]</c> 決定，
///     Service 不重複做角色檢查；但會做「資料所有權」檢查
///     （例：<see cref="GetMineAsync"/> 強制以 <paramref name="studentId"/> 為 WHERE 主條件）
/// </summary>
public interface IAbsenceRecordService
{
    /// <summary>
    /// 新增一筆曠課紀錄（行政使用）。
    ///
    /// 驗證：
    ///   ‧ <paramref name="input"/> 的 <c>StudentId</c> 必須存在且擁有 Student 角色，
    ///     否則回傳 <see cref="AbsenceRecordActionResult.Success"/> = false。
    ///   ‧ <c>Note</c> 若全為空白，會被寫入為 <c>null</c>；否則會 <c>Trim()</c> 後寫入。
    ///
    /// 稽核：<c>CreatedByUserId</c> 由 <paramref name="createdByUserId"/> 帶入，
    /// <c>CreatedAt</c> 一律以 <c>DateTime.UtcNow</c> 覆寫（不依賴 Entity 建構期預設）。
    /// </summary>
    /// <param name="input">表單輸入。已通過 <c>ModelState</c> 驗證。</param>
    /// <param name="createdByUserId">登錄者 UserId（Controller 從 Cookie 取）。</param>
    Task<AbsenceRecordActionResult> CreateAsync(AbsenceRecordCreateInput input, int createdByUserId);

    /// <summary>
    /// 更新一筆曠課紀錄（行政使用）。
    ///
    /// 驗證同 <see cref="CreateAsync"/>；紀錄不存在時回傳失敗。
    /// <c>CreatedByUserId</c> / <c>CreatedAt</c> **不會被覆寫**（保留原稽核資訊）。
    /// </summary>
    Task<AbsenceRecordActionResult> UpdateAsync(AbsenceRecordEditInput input);

    /// <summary>
    /// 刪除一筆曠課紀錄（行政使用）。紀錄不存在時回傳失敗。
    /// </summary>
    Task<AbsenceRecordActionResult> DeleteAsync(int id);

    /// <summary>
    /// 取得行政清單頁完整資料（含篩選結果、總時數與下拉選項）。
    ///
    /// 排序：<c>OccurredAt DESC</c>（最新在最上）。
    ///
    /// 日期邊界：<c>query.DateTo</c> 若只帶到日期，Service 會補到當日 <c>23:59:59</c>
    /// 以「含當天」為語意（體例對齊 Phase 5 <c>AdminReportQuery.DateTo</c>）。
    ///
    /// <see cref="AbsenceRecordAdminViewModel.TotalHours"/> = 當前篩選結果的加總。
    /// </summary>
    Task<AbsenceRecordAdminViewModel> GetForAdminAsync(AbsenceRecordAdminQuery query);

    /// <summary>
    /// 取得指定學員的曠課紀錄（學員自查）。
    ///
    /// 安全性：<paramref name="studentId"/> 一律作為 WHERE 主條件，
    /// 學員即便偽造 <paramref name="query"/> 內容也無法讀到他人資料。
    /// Controller 應以「當前登入者的 UserId」呼叫本方法。
    ///
    /// <see cref="MyAbsenceRecordViewModel.TotalHours"/> = **該學員終身總曠課時數**，
    /// 刻意不受 <paramref name="query"/> 日期篩選影響，讓學員每次進頁面都能看到
    /// 一個「終身總計」的自我警惕數字。
    /// </summary>
    /// <param name="studentId">學員 UserId（Controller 從 Cookie 取）。</param>
    /// <param name="query">日期篩選條件（皆選填）。</param>
    Task<MyAbsenceRecordViewModel> GetMineAsync(int studentId, MyAbsenceRecordQuery query);

    /// <summary>
    /// 依 Id 載入一筆曠課紀錄，轉為編輯表單模型。
    /// 找不到時回傳 <c>null</c>；找到時同時填入 <see cref="AbsenceRecordEditInput.StudentOptions"/>
    /// 讓 Controller 可直接把物件交給 View（一次查完不必二次呼叫）。
    /// </summary>
    Task<AbsenceRecordEditInput?> GetForEditAsync(int id);

    /// <summary>
    /// 取得學員下拉選項（所有擁有 Student 角色的使用者，含停用者，
    /// 因為歷史紀錄可能綁在已停用的學員身上）。
    /// 供 Controller 在 <c>Create GET</c> 或 <c>ModelState.IsValid == false</c>
    /// 需要重補下拉時呼叫。
    /// </summary>
    Task<List<UserOption>> GetStudentOptionsAsync();
}

/// <summary>
/// 曠課紀錄操作結果。
/// Success=false 時，<see cref="ErrorMessage"/> 會帶可顯示給使用者的錯誤訊息。
/// <see cref="RecordId"/> 僅在 Create 成功時有值。
/// </summary>
public record AbsenceRecordActionResult(bool Success, string? ErrorMessage, int? RecordId = null);
