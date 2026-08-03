namespace LeaveSystem.Services.Email;

/// <summary>
/// Email 發送模式。
/// </summary>
public enum EmailMode
{
    /// <summary>
    /// 落地到 PickupDirectory 目錄，不真的寄出（適合開發／測試）。
    /// </summary>
    PickupDirectory = 0,

    /// <summary>
    /// 使用 SMTP 真的寄出。
    /// </summary>
    Smtp = 1
}

/// <summary>
/// Email 設定（對應 appsettings.json 的 "Email" 區段）。
/// </summary>
public class EmailOptions
{
    public EmailMode Mode { get; set; } = EmailMode.PickupDirectory;

    /// <summary>
    /// PickupDirectory 模式下的目錄路徑。
    /// </summary>
    public string PickupDirectory { get; set; } = "./mail-pickup";

    /// <summary>
    /// 寄件者 Email。
    /// </summary>
    public string From { get; set; } = "noreply@leavesystem.local";

    /// <summary>
    /// SMTP 設定（Mode=Smtp 時使用）。
    /// </summary>
    public SmtpOptions Smtp { get; set; } = new();
}

public class SmtpOptions
{
    public string Host { get; set; } = "localhost";
    public int Port { get; set; } = 25;
    public bool UseSsl { get; set; } = false;
    public string? Username { get; set; }
    public string? Password { get; set; }
}
