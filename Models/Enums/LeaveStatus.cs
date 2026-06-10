namespace LeaveSystem.Models.Enums;

/// <summary>
/// 請假申請的整體狀態。
/// 狀態流轉順序：Draft → Pending → (Approved | Rejected | Cancelled)
/// </summary>
public enum LeaveStatus
{
    /// <summary>
    /// 草稿：學員建立申請但尚未送出。可繼續編輯或刪除。
    /// </summary>
    Draft = 0,

    /// <summary>
    /// 簽核中：已送出，等待簽核人決定。
    /// 此狀態下會搭配 LeaveRequest.CurrentLevel 表示目前停在第幾關。
    /// </summary>
    Pending = 1,

    /// <summary>
    /// 已核准：所有層級皆核准通過，請假時數已計入學員累計。
    /// </summary>
    Approved = 2,

    /// <summary>
    /// 已駁回：任一層級簽核人駁回，整張單終止。學員可重新提交新單。
    /// </summary>
    Rejected = 3,

    /// <summary>
    /// 已撤銷：學員主動撤回申請（僅在尚未進入下一級簽核前允許）。
    /// </summary>
    Cancelled = 4
}
