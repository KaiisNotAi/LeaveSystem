using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.ViewModels;

// ═══════════════════════════════════════════════════════════════
// Phase 6 — 曠課紀錄 ViewModels
//
// 一個檔集中放本 Phase 的所有 ViewModel，體例對齊
// LeaveReportViewModels.cs 與 CohortAdminViewModels.cs。
//
// 下拉選項刻意不在此檔重新定義，直接重用既有的：
//   - UserOption   ：CohortAdminViewModels.cs
//   - CohortOption ：UserAdminViewModels.cs
// ═══════════════════════════════════════════════════════════════

// ─────────────────────────────────────────────────────────────
// 1. 行政新增曠課（/Admin/AbsenceRecords/Create）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 行政後台「新增曠課紀錄」表單。
///
/// 業務規則：
///   - 曠課時數最小單位 1 小時（見 <c>docs/01 §3.1</c>），不接受 0.5、1.5 等小數；
///     Entity 上以 <c>[Range(1, 100)]</c> 擋範圍，本 ViewModel 額外用
///     <see cref="IValidatableObject"/> 擋「非整數」。
///   - 學員（StudentId）由行政從下拉選擇，因此後端須驗證該 Id 確實存在且為
///     Student 角色（此檢查在 Service 層做，見 Phase 6 IAbsenceRecordService）。
/// </summary>
public class AbsenceRecordCreateInput : IValidatableObject
{
    [Required(ErrorMessage = "請選擇學員")]
    [Display(Name = "學員")]
    public int StudentId { get; set; }

    /// <summary>
    /// 曠課發生時間（含日期與上課時段的開始時間）。
    /// View 用 <c>&lt;input type="datetime-local"&gt;</c> 讓行政直接挑日期時間。
    /// </summary>
    [Required(ErrorMessage = "請選擇發生時間")]
    [DataType(DataType.DateTime)]
    [Display(Name = "發生時間")]
    public DateTime OccurredAt { get; set; } = DateTime.Today.AddHours(8);

    /// <summary>
    /// 曠課時數（正整數 1~100）。
    /// </summary>
    [Required(ErrorMessage = "請輸入曠課時數")]
    [Range(1, 100, ErrorMessage = "曠課時數必須介於 1 ~ 100 小時")]
    [Display(Name = "曠課時數")]
    public decimal Hours { get; set; } = 1;

    [StringLength(500, ErrorMessage = "備註最長 500 字")]
    [Display(Name = "備註")]
    public string? Note { get; set; }

    /// <summary>學員下拉選項（由 Controller 填入）。</summary>
    public List<UserOption> StudentOptions { get; set; } = new();

    /// <summary>
    /// 額外驗證：曠課時數必須為整數（§3.1「最小單位 1 小時」）。
    /// <see cref="Range"/> 只能擋 1~100 的範圍，擋不掉 1.5 這種小數，
    /// 因此以 <see cref="IValidatableObject"/> 補一條伺服器端規則。
    /// </summary>
    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Hours != Math.Floor(Hours))
        {
            yield return new ValidationResult(
                "曠課時數必須為正整數（不接受小數）",
                new[] { nameof(Hours) });
        }
    }
}

// ─────────────────────────────────────────────────────────────
// 2. 行政編輯曠課（/Admin/AbsenceRecords/Edit/{id}）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 行政後台「編輯曠課紀錄」表單。與 <see cref="AbsenceRecordCreateInput"/> 差別在多一個 Id，
/// 其餘欄位驗證規則相同（含整數限制）。
/// </summary>
public class AbsenceRecordEditInput : IValidatableObject
{
    public int Id { get; set; }

    [Required(ErrorMessage = "請選擇學員")]
    [Display(Name = "學員")]
    public int StudentId { get; set; }

    [Required(ErrorMessage = "請選擇發生時間")]
    [DataType(DataType.DateTime)]
    [Display(Name = "發生時間")]
    public DateTime OccurredAt { get; set; }

    [Required(ErrorMessage = "請輸入曠課時數")]
    [Range(1, 100, ErrorMessage = "曠課時數必須介於 1 ~ 100 小時")]
    [Display(Name = "曠課時數")]
    public decimal Hours { get; set; }

    [StringLength(500, ErrorMessage = "備註最長 500 字")]
    [Display(Name = "備註")]
    public string? Note { get; set; }

    public List<UserOption> StudentOptions { get; set; } = new();

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Hours != Math.Floor(Hours))
        {
            yield return new ValidationResult(
                "曠課時數必須為正整數（不接受小數）",
                new[] { nameof(Hours) });
        }
    }
}

// ─────────────────────────────────────────────────────────────
// 3. 行政清單查詢（/Admin/AbsenceRecords/Index）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 行政清單頁的篩選條件。皆選填，未填即代表該維度不限。
/// 表單以 GET 送出，讓查詢條件保留在 URL 上方便複製/加書籤（體例對齊 Phase 5 Reports）。
///
/// 注意：曠課紀錄沒有「假別」維度，因此沒有 <c>LeaveTypeId</c>。
/// </summary>
public class AbsenceRecordAdminQuery
{
    [Display(Name = "班期")]
    public int? CohortId { get; set; }

    [Display(Name = "學員")]
    public int? StudentId { get; set; }

    /// <summary>
    /// 起始日期（含）。對應條件：<c>a.OccurredAt &gt;= DateFrom</c>。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期起")]
    public DateTime? DateFrom { get; set; }

    /// <summary>
    /// 結束日期（含）。若只帶到日期，Service 會補到當日 <c>23:59:59</c>
    /// 以「含當天」為語意（體例對齊 Phase 5 <c>AdminReportQuery.DateTo</c>）。
    /// </summary>
    [DataType(DataType.Date)]
    [Display(Name = "日期迄")]
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// 行政清單頁單列資料。
/// 顯示所需資訊：學員姓名、班期名稱（可為 null 若學員未指派班期）、
/// 發生時間、時數、備註、登錄者、登錄時間。
/// </summary>
public class AbsenceRecordListItem
{
    public int Id { get; set; }

    /// <summary>學員顯示名稱。</summary>
    public string StudentDisplayName { get; set; } = string.Empty;

    /// <summary>學員所屬班期名稱；學員未指派班期時為 <c>null</c>。</summary>
    public string? CohortName { get; set; }

    public DateTime OccurredAt { get; set; }
    public decimal Hours { get; set; }
    public string? Note { get; set; }

    /// <summary>登錄此紀錄的行政人員顯示名稱（稽核用）。</summary>
    public string CreatedByDisplayName { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 行政清單頁完整資料（帶篩選條件、結果列、下拉選項與總計）。
/// </summary>
public class AbsenceRecordAdminViewModel
{
    public AbsenceRecordAdminQuery Query { get; set; } = new();
    public List<AbsenceRecordListItem> Items { get; set; } = new();

    /// <summary>目前篩選結果的曠課總時數（供 View 摘要顯示）。</summary>
    public decimal TotalHours { get; set; }

    // ─── 下拉選項（Controller 填入） ───
    public List<CohortOption> CohortOptions { get; set; } = new();
    public List<UserOption> StudentOptions { get; set; } = new();
}

// ─────────────────────────────────────────────────────────────
// 4. 學員自查（/AbsenceRecords）
// ─────────────────────────────────────────────────────────────

/// <summary>
/// 學員自查篩選條件。僅提供日期區間，不提供學員維度（Service 強制以登入者 Id 過濾）。
/// </summary>
public class MyAbsenceRecordQuery
{
    [DataType(DataType.Date)]
    [Display(Name = "日期起")]
    public DateTime? DateFrom { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "日期迄")]
    public DateTime? DateTo { get; set; }
}

/// <summary>
/// 學員自查單列資料。
/// 刻意不含「登錄者」欄位：對學員而言登錄者屬內部審計資訊，非其查詢重點。
/// </summary>
public class MyAbsenceRecordListItem
{
    public int Id { get; set; }
    public DateTime OccurredAt { get; set; }
    public decimal Hours { get; set; }
    public string? Note { get; set; }
    public DateTime CreatedAt { get; set; }
}

/// <summary>
/// 學員自查頁完整資料。
/// </summary>
public class MyAbsenceRecordViewModel
{
    public MyAbsenceRecordQuery Query { get; set; } = new();
    public List<MyAbsenceRecordListItem> Items { get; set; } = new();

    /// <summary>
    /// 該學員截至目前的曠課總時數（**不受日期篩選影響**）。
    /// 目的是讓學員每次進頁面都能看到一個「終身總計」的自我警惕數字。
    /// </summary>
    public decimal TotalHours { get; set; }
}
