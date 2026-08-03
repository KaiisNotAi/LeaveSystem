namespace LeaveSystem.Services.Email;

/// <summary>
/// Email 發送介面。失敗不應擲例外，僅回傳 false（呼叫端可視情況記 log）。
/// </summary>
public interface IEmailSender
{
    /// <summary>
    /// 寄送一封純文字 Email。
    /// </summary>
    /// <returns>成功為 true，失敗為 false。</returns>
    Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default);
}
