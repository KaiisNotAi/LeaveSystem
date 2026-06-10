namespace LeaveSystem.Models.Enums;

/// <summary>
/// 單一簽核步驟（LeaveRequestStep）的決定結果。
/// 注意：這是「單一關卡」的狀態，不是整張請假單的狀態。
/// 整張單的狀態請看 <see cref="LeaveStatus"/>。
/// </summary>
public enum ApprovalDecision
{
    /// <summary>
    /// 待處理：此關卡尚未被簽核人處理。
    /// 申請送出後，所有預定關卡會先以此狀態建立，逐關喚醒。
    /// </summary>
    Pending = 0,

    /// <summary>
    /// 核准：簽核人同意此申請，可繼續往下一關（若還有的話）。
    /// </summary>
    Approved = 1,

    /// <summary>
    /// 駁回：簽核人否決，整張單立即進入 LeaveStatus.Rejected。
    /// 駁回時必須填寫 Comment（駁回原因）。
    /// </summary>
    Rejected = 2
}
