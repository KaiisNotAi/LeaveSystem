using LeaveSystem.Services.Email;

namespace LeaveSystem.Tests.Helpers;

/// <summary>
/// 測試用 fake email sender。記錄每次呼叫，並可設定成失敗以驗證上層行為。
/// </summary>
public class FakeEmailSender : IEmailSender
{
    public List<(string To, string Subject, string Body)> Sent { get; } = new();
    public bool ReturnValue { get; set; } = true;
    public bool ThrowOnSend { get; set; } = false;

    public Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        if (ThrowOnSend)
        {
            throw new InvalidOperationException("模擬 email 例外");
        }
        Sent.Add((toEmail, subject, body));
        return Task.FromResult(ReturnValue);
    }
}
