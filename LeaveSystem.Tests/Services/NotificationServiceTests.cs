using LeaveSystem.Services;
using LeaveSystem.Tests.Helpers;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 7 站內通知服務規格測試。
/// 涵蓋：建立、查詢、未讀計數、標記已讀、批次已讀、跨 UserId 隔離。
/// </summary>
public class NotificationServiceTests
{
    [Fact]
    public async Task CreateAsync_應建立未讀通知並回傳Id()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        var id = await sut.CreateAsync(userId: 10, title: "T", message: "M", url: "/u");

        Assert.True(id > 0);
        var saved = await db.Notifications.FindAsync(id);
        Assert.NotNull(saved);
        Assert.Equal(10, saved!.UserId);
        Assert.Equal("T", saved.Title);
        Assert.Equal("M", saved.Message);
        Assert.Equal("/u", saved.Url);
        Assert.False(saved.IsRead);
        Assert.Null(saved.ReadAt);
        Assert.True((DateTime.UtcNow - saved.CreatedAt).TotalMinutes < 1);
    }

    [Fact]
    public async Task GetForUserAsync_應只回傳該使用者的通知並依建立時間新到舊排序()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        await sut.CreateAsync(10, "old", "m", null);
        await Task.Delay(10);
        await sut.CreateAsync(10, "new", "m", null);
        await sut.CreateAsync(20, "other", "m", null);

        var items = await sut.GetForUserAsync(10);

        Assert.Equal(2, items.Count);
        Assert.Equal("new", items[0].Title);
        Assert.Equal("old", items[1].Title);
    }

    [Fact]
    public async Task GetUnreadCountAsync_應僅計算未讀且屬於該使用者的數量()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        var id1 = await sut.CreateAsync(10, "a", "m", null);
        await sut.CreateAsync(10, "b", "m", null);
        await sut.CreateAsync(20, "c", "m", null);

        await sut.MarkReadAsync(id1, userId: 10);

        Assert.Equal(1, await sut.GetUnreadCountAsync(10));
        Assert.Equal(1, await sut.GetUnreadCountAsync(20));
        Assert.Equal(0, await sut.GetUnreadCountAsync(99));
    }

    [Fact]
    public async Task MarkReadAsync_應設定IsRead與ReadAt()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        var id = await sut.CreateAsync(10, "t", "m", null);

        var ok = await sut.MarkReadAsync(id, userId: 10);

        Assert.True(ok);
        var n = await db.Notifications.FindAsync(id);
        Assert.True(n!.IsRead);
        Assert.NotNull(n.ReadAt);
    }

    [Fact]
    public async Task MarkReadAsync_非通知擁有者_應回傳False且不變更()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        var id = await sut.CreateAsync(10, "t", "m", null);

        var ok = await sut.MarkReadAsync(id, userId: 99);

        Assert.False(ok);
        var n = await db.Notifications.FindAsync(id);
        Assert.False(n!.IsRead);
        Assert.Null(n.ReadAt);
    }

    [Fact]
    public async Task MarkAllReadAsync_應將該使用者所有未讀標記為已讀_不影響他人()
    {
        using var db = InMemoryDbContextFactory.CreateEmpty();
        var sut = new NotificationService(db);

        await sut.CreateAsync(10, "a", "m", null);
        await sut.CreateAsync(10, "b", "m", null);
        var otherId = await sut.CreateAsync(20, "c", "m", null);

        var affected = await sut.MarkAllReadAsync(10);

        Assert.Equal(2, affected);
        Assert.Equal(0, await sut.GetUnreadCountAsync(10));

        var other = await db.Notifications.FindAsync(otherId);
        Assert.False(other!.IsRead);
    }
}
