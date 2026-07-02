namespace LeaveSystem.Services;

/// <summary>
/// 請假時數計算服務介面。
/// 負責把起訖時間換算成「實際請假時數」（扣除午休與非上課時段）。
/// </summary>
public interface ILeaveCalculator
{
    /// <summary>
    /// 嘗試計算請假時數。
    /// </summary>
    /// <param name="startAt">請假開始時間（整點）。</param>
    /// <param name="endAt">請假結束時間（整點，且需晚於開始）。</param>
    /// <param name="totalHours">計算出的總時數。</param>
    /// <param name="errorMessage">若失敗，回傳可顯示給使用者的錯誤訊息。</param>
    /// <returns>成功回傳 true，失敗回傳 false。</returns>
    bool TryCalculate(DateTime startAt, DateTime endAt, out decimal totalHours, out string? errorMessage);
}
