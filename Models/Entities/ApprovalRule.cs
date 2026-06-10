using System.ComponentModel.DataAnnotations;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 簽核規則實體。
/// 由 Admin 後台維護，定義「請假時數落在某區間時要走哪幾關」。
///
/// 預設種子資料（SeedData）：
///   |  區間 (MinHours ~ MaxHours)  |  RequiredRoles                    |
///   |  0 ~ 8                       |  ["Tutor"]                         |
///   |  8.01 ~ 24                   |  ["Tutor","SectionChief"]          |
///   |  24.01 ~ null（無上限）      |  ["Tutor","SectionChief","BranchDirector"] |
///
/// 設計理由：
/// 1. 把規則存資料庫而非寫死於 C#，未來想調整門檻或關卡只要改一筆 row。
/// 2. RequiredRoles 用 JSON 字串而非另開橋接表，是因為角色順序很重要（先導師再科長），
///    JSON 陣列可天然保留順序，又只需要單欄位即可，邏輯簡單。
/// </summary>
public class ApprovalRule
{
    public int Id { get; set; }

    /// <summary>
    /// 區間下限（含）。例如此規則為 "0 ~ 8 小時"，MinHours = 0。
    /// </summary>
    [Range(0, 1000)]
    public decimal MinHours { get; set; }

    /// <summary>
    /// 區間上限（含）。null 代表「無上限」（最高層級規則）。
    /// </summary>
    [Range(0, 1000)]
    public decimal? MaxHours { get; set; }

    /// <summary>
    /// 所需簽核角色順序，以 JSON 陣列字串儲存。
    /// 例如：["Tutor"]、["Tutor","SectionChief"]、["Tutor","SectionChief","BranchDirector"]。
    /// 服務層會以 System.Text.Json 反序列化成 string[] 後逐一派送。
    /// </summary>
    [Required]
    [StringLength(500)]
    public string RequiredRoles { get; set; } = "[]";

    /// <summary>
    /// 規則描述（給管理員看，如「8 小時內由導師決行」）。
    /// </summary>
    [StringLength(200)]
    public string? Description { get; set; }
}
