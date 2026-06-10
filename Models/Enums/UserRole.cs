namespace LeaveSystem.Models.Enums;

/// <summary>
/// 系統角色列舉。
/// 一個帳號可同時擁有多個角色（透過 UserRole 多對多橋接表）。
/// 例如：某位導師同時也兼任科長，他的帳號就會掛 Tutor + SectionChief 兩個角色。
/// </summary>
public enum UserRole
{
    /// <summary>
    /// 系統管理員：可管理所有資料、使用者、班期、簽核規則。權限最大。
    /// </summary>
    Admin = 1,

    /// <summary>
    /// 行政人員：負責建立帳號、班期、假別、登錄曠課時數、查看報表。
    /// 不參與簽核流程，但可代為管理。
    /// </summary>
    Staff = 2,

    /// <summary>
    /// 導師：第一線簽核人，負責所屬班期學員的請假申請。
    /// 任何時數的請假，第一關都由導師處理。
    /// </summary>
    Tutor = 3,

    /// <summary>
    /// 科長：第二級簽核人。當請假時數超過導師可決行門檻時介入。
    /// </summary>
    SectionChief = 4,

    /// <summary>
    /// 分署長：最高層簽核人。當請假時數超過科長可決行門檻時介入。
    /// </summary>
    BranchDirector = 5,

    /// <summary>
    /// 學員：可提交請假申請、查詢自己的紀錄與累計時數。
    /// 必須隸屬於某個班期。
    /// </summary>
    Student = 6
}
