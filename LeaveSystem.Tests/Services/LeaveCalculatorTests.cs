using LeaveSystem.Services;

namespace LeaveSystem.Tests.Services;

public class LeaveCalculatorTests
{
    private readonly LeaveCalculator _sut = new();

    [Fact]
    public void TryCalculate_08To09_ShouldReturn1Hour()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 8, 0, 0),
            new DateTime(2026, 6, 9, 9, 0, 0),
            out var totalHours,
            out var error);

        Assert.True(ok);
        Assert.Equal(1m, totalHours);
        Assert.Null(error);
    }

    [Fact]
    public void TryCalculate_11To14_ShouldSkipLunchAndReturn2Hours()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 11, 0, 0),
            new DateTime(2026, 6, 9, 14, 0, 0),
            out var totalHours,
            out var error);

        Assert.True(ok);
        Assert.Equal(2m, totalHours);
        Assert.Null(error);
    }

    [Fact]
    public void TryCalculate_08To17_ShouldReturn8Hours()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 8, 0, 0),
            new DateTime(2026, 6, 9, 17, 0, 0),
            out var totalHours,
            out var error);

        Assert.True(ok);
        Assert.Equal(8m, totalHours);
        Assert.Null(error);
    }

    [Fact]
    public void TryCalculate_CrossDay_13ToNextDay12_ShouldReturn8Hours()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 13, 0, 0),
            new DateTime(2026, 6, 10, 12, 0, 0),
            out var totalHours,
            out var error);

        Assert.True(ok);
        Assert.Equal(8m, totalHours);
        Assert.Null(error);
    }

    [Fact]
    public void TryCalculate_StartNotOnHour_ShouldFail()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 8, 30, 0),
            new DateTime(2026, 6, 9, 9, 0, 0),
            out var totalHours,
            out var error);

        Assert.False(ok);
        Assert.Equal(0m, totalHours);
        Assert.Equal("請假時間必須使用整點（例如 08:00、13:00）。", error);
    }

    [Fact]
    public void TryCalculate_EndEarlierThanStart_ShouldFail()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 10, 0, 0),
            new DateTime(2026, 6, 9, 9, 0, 0),
            out var totalHours,
            out var error);

        Assert.False(ok);
        Assert.Equal(0m, totalHours);
        Assert.Equal("結束時間必須晚於開始時間。", error);
    }

    [Fact]
    public void TryCalculate_OutsideSelectableHour_ShouldFail()
    {
        var ok = _sut.TryCalculate(
            new DateTime(2026, 6, 9, 7, 0, 0),
            new DateTime(2026, 6, 9, 9, 0, 0),
            out var totalHours,
            out var error);

        Assert.False(ok);
        Assert.Equal(0m, totalHours);
        Assert.Equal("請假時間需介於每日 08:00 到 17:00 的整點時段。", error);
    }
}
