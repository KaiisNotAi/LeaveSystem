using LeaveSystem.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Email 發送規格測試。
/// PickupDirectory 模式應在指定資料夾產生 .eml 檔，內容含收件人/主旨/內文。
/// </summary>
public class EmailSenderTests
{
    [Fact]
    public async Task NullEmailSender_應永遠成功且不擲例外()
    {
        var sut = new NullEmailSender();
        var ok = await sut.SendAsync("a@b.c", "s", "body");
        Assert.True(ok);
    }

    [Fact]
    public async Task MailKitEmailSender_PickupDirectory_應寫入eml檔()
    {
        var dir = Path.Combine(Path.GetTempPath(), "leavesystem-mail-test-" + Guid.NewGuid());
        try
        {
            var opts = Options.Create(new EmailOptions
            {
                Mode = EmailMode.PickupDirectory,
                PickupDirectory = dir,
                From = "noreply@test.local"
            });
            var sut = new MailKitEmailSender(opts, NullLogger<MailKitEmailSender>.Instance);

            var ok = await sut.SendAsync("stu@test.local", "測試主旨", "測試內文");

            Assert.True(ok);
            Assert.True(Directory.Exists(dir));
            var files = Directory.GetFiles(dir, "*.eml");
            Assert.Single(files);
            var content = await File.ReadAllTextAsync(files[0]);
            Assert.Contains("stu@test.local", content);
            Assert.Contains("noreply@test.local", content);
            // Subject 於 .eml 內可能為 Base64/QP 編碼中文，僅檢查 header 存在
            Assert.Contains("Subject", content, StringComparison.OrdinalIgnoreCase);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }

    [Fact]
    public async Task MailKitEmailSender_收件人格式錯誤_應回傳False不擲例外()
    {
        var dir = Path.Combine(Path.GetTempPath(), "leavesystem-mail-test-" + Guid.NewGuid());
        try
        {
            var opts = Options.Create(new EmailOptions
            {
                Mode = EmailMode.PickupDirectory,
                PickupDirectory = dir,
                From = "noreply@test.local"
            });
            var sut = new MailKitEmailSender(opts, NullLogger<MailKitEmailSender>.Instance);

            var ok = await sut.SendAsync(toEmail: "not-an-email", subject: "s", body: "b");

            Assert.False(ok);
        }
        finally
        {
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }
    }
}
