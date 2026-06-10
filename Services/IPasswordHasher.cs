namespace LeaveSystem.Services;

/// <summary>
/// 密碼雜湊服務介面。
///
/// 為什麼需要雜湊？
/// 直接把使用者密碼存進資料庫是嚴重的安全問題，萬一資料庫外洩，
/// 所有使用者的密碼就一起洩漏。雜湊是「不可逆」的轉換，例如：
///   原密碼 Admin@123  →  $2a$11$abcdef.....（雜湊值）
/// 不可能從雜湊值推回原密碼，但同一原密碼產生的雜湊每次比對都會通過。
///
/// 為什麼不用 SHA-256 之類？
/// 一般雜湊演算法太快，駭客可以用「字典攻擊」每秒嘗試數十億組密碼。
/// BCrypt 是「慢雜湊」，每次計算都要花一些時間（約 0.1 秒），
/// 而且自帶「鹽值（salt）」每次產生都不一樣，讓字典攻擊失效。
/// </summary>
public interface IPasswordHasher
{
    /// <summary>
    /// 把明碼轉成雜湊字串。
    /// </summary>
    string Hash(string plainPassword);

    /// <summary>
    /// 驗證明碼是否與雜湊相符。
    /// </summary>
    bool Verify(string plainPassword, string hashedPassword);
}
