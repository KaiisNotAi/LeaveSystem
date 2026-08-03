namespace LeaveSystem.Services.Email;

/// <summary>
/// 空實作，測試與退路使用。永遠回傳 true 但不做任何事。
/// </summary>
public class NullEmailSender : IEmailSender
{
    public Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
        => Task.FromResult(true);
}
