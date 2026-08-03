using MailKit.Net.Smtp;
using MailKit.Security;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MimeKit;

namespace LeaveSystem.Services.Email;

/// <summary>
/// 以 MailKit 實作的 Email 發送器。
/// - Mode=PickupDirectory：將郵件寫成 .eml 檔案落地到指定目錄，不真的寄出。
/// - Mode=Smtp：使用 SmtpClient 連線寄出。
/// 任何例外皆吞掉並回傳 false，避免影響上層業務流程。
/// </summary>
public class MailKitEmailSender : IEmailSender
{
    private readonly EmailOptions _options;
    private readonly ILogger<MailKitEmailSender> _logger;

    public MailKitEmailSender(IOptions<EmailOptions> options, ILogger<MailKitEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public async Task<bool> SendAsync(string toEmail, string subject, string body, CancellationToken cancellationToken = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(toEmail) || !toEmail.Contains('@'))
            {
                _logger.LogWarning("Email 收件人格式無效：{To}", toEmail);
                return false;
            }

            var message = new MimeMessage();
            message.From.Add(MailboxAddress.Parse(_options.From));
            message.To.Add(MailboxAddress.Parse(toEmail));
            message.Subject = subject;
            message.Body = new TextPart("plain") { Text = body };

            if (_options.Mode == EmailMode.PickupDirectory)
            {
                Directory.CreateDirectory(_options.PickupDirectory);
                var fileName = $"{DateTime.UtcNow:yyyyMMddHHmmssfff}-{Guid.NewGuid():N}.eml";
                var path = Path.Combine(_options.PickupDirectory, fileName);
                await using var stream = File.Create(path);
                await message.WriteToAsync(stream, cancellationToken);
                _logger.LogInformation("Email 已寫入 PickupDirectory：{Path}", path);
                return true;
            }

            using var client = new SmtpClient();
            var secure = _options.Smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
            await client.ConnectAsync(_options.Smtp.Host, _options.Smtp.Port, secure, cancellationToken);
            if (!string.IsNullOrWhiteSpace(_options.Smtp.Username))
            {
                await client.AuthenticateAsync(_options.Smtp.Username, _options.Smtp.Password ?? string.Empty, cancellationToken);
            }
            await client.SendAsync(message, cancellationToken);
            await client.DisconnectAsync(true, cancellationToken);
            _logger.LogInformation("Email 已透過 SMTP 寄出至 {To}", toEmail);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Email 寄送失敗（收件人：{To}，主旨：{Subject}）", toEmail, subject);
            return false;
        }
    }
}
