namespace LeaveSystem.Services;

/// <summary>
/// 使用 BCrypt.Net-Next 套件實作的密碼雜湊器。
/// </summary>
public class BcryptPasswordHasher : IPasswordHasher
{
    /// <summary>
    /// 把明碼轉成 BCrypt 雜湊字串（含 salt）。
    /// </summary>
    public string Hash(string plainPassword)
    {
        return BCrypt.Net.BCrypt.HashPassword(plainPassword);
    }

    /// <summary>
    /// 驗證明碼與雜湊是否相符。
    /// 注意 BCrypt 的 Verify 會自動從雜湊字串解析出 salt 進行比對。
    /// </summary>
    public bool Verify(string plainPassword, string hashedPassword)
    {
        if (string.IsNullOrEmpty(hashedPassword)) return false;

        try
        {
            return BCrypt.Net.BCrypt.Verify(plainPassword, hashedPassword);
        }
        catch
        {
            // 雜湊字串格式錯誤（例如雜湊已損壞）時直接視為不通過
            return false;
        }
    }
}
