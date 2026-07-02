namespace LeaveSystem.Services;

/// <summary>
/// 請假時數計算服務。
///
/// 規則：
/// 1. 上課時段為 08:00~12:00、13:00~17:00。
/// 2. 午休 12:00~13:00 不計。
/// 3. 可跨日，系統會逐日計算與上課時段重疊的時數。
/// 4. 最小單位為 1 小時（搭配前端整點下拉）。
/// </summary>
public class LeaveCalculator : ILeaveCalculator
{
    public bool TryCalculate(DateTime startAt, DateTime endAt, out decimal totalHours, out string? errorMessage)
    {
        totalHours = 0m;
        errorMessage = null;

        // 起訖必須是整點，避免出現 08:30 這種半小時切分。
        if (!IsOnTheHour(startAt) || !IsOnTheHour(endAt))
        {
            errorMessage = "請假時間必須使用整點（例如 08:00、13:00）。";
            return false;
        }

        if (endAt <= startAt)
        {
            errorMessage = "結束時間必須晚於開始時間。";
            return false;
        }

        // 本系統學員下拉選單只提供 08~17，因此伺服器端也做同樣限制，避免繞過前端。
        if (!IsSelectableHour(startAt.Hour) || !IsSelectableHour(endAt.Hour))
        {
            errorMessage = "請假時間需介於每日 08:00 到 17:00 的整點時段。";
            return false;
        }

        var requestRange = (Start: startAt, End: endAt);

        for (var day = startAt.Date; day <= endAt.Date; day = day.AddDays(1))
        {
            // 上午時段：08:00~12:00
            totalHours += OverlapHours(requestRange, (day.AddHours(8), day.AddHours(12)));

            // 下午時段：13:00~17:00
            totalHours += OverlapHours(requestRange, (day.AddHours(13), day.AddHours(17)));
        }

        // 若完全沒有與上課時段重疊（例如只選到午休），視為無效申請。
        if (totalHours <= 0)
        {
            errorMessage = "請假區間未涵蓋任何上課時段，請重新選擇。";
            return false;
        }

        return true;
    }

    private static bool IsOnTheHour(DateTime dt)
    {
        return dt.Minute == 0 && dt.Second == 0 && dt.Millisecond == 0;
    }

    private static bool IsSelectableHour(int hour)
    {
        return hour >= 8 && hour <= 17;
    }

    private static decimal OverlapHours((DateTime Start, DateTime End) a, (DateTime Start, DateTime End) b)
    {
        var start = a.Start > b.Start ? a.Start : b.Start;
        var end = a.End < b.End ? a.End : b.End;

        if (end <= start)
        {
            return 0m;
        }

        return (decimal)(end - start).TotalHours;
    }
}
