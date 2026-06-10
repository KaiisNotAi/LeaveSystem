using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace LeaveSystem.Models.Entities;

/// <summary>
/// 班期實體（一期一班）。
/// 例如：「113-1 資安班」、「113-2 雲端班」。
/// 每個班期有自己的總上課時數、請假上限百分比，以及對應的導師、科長、分署長。
/// </summary>
public class Cohort
{
    public int Id { get; set; }

    /// <summary>
    /// 班期名稱（如「113-1 資安班」）。
    /// </summary>
    [Required(ErrorMessage = "班期名稱為必填")]
    [StringLength(100, ErrorMessage = "班期名稱最長 100 字")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// 開課日期。
    /// </summary>
    [Required]
    [DataType(DataType.Date)]
    public DateTime StartDate { get; set; }

    /// <summary>
    /// 結訓日期。
    /// </summary>
    [Required]
    [DataType(DataType.Date)]
    public DateTime EndDate { get; set; }

    /// <summary>
    /// 整期總上課時數（如 900）。用於計算請假上限。
    /// </summary>
    [Range(1, 10000, ErrorMessage = "總上課時數必須介於 1 ~ 10000")]
    public int TotalHours { get; set; }

    /// <summary>
    /// 請假上限百分比（預設 10，代表 10%）。
    /// 規定：學員整期總請假時數不得超過總上課時數的此百分比。
    /// </summary>
    [Range(0, 100, ErrorMessage = "百分比必須介於 0 ~ 100")]
    public int LeaveLimitPercent { get; set; } = 10;

    /// <summary>
    /// 計算屬性：請假時數上限。不存進資料庫，每次讀取時即時計算。
    /// 例如：總時數 900 × 10% = 90 小時。
    /// </summary>
    [NotMapped]
    public int LeaveLimitHours => (int)Math.Floor(TotalHours * LeaveLimitPercent / 100.0);

    /// <summary>
    /// 此班期的導師 UserId。第一線簽核人。
    /// </summary>
    public int? TutorUserId { get; set; }

    /// <summary>
    /// 此班期的科長 UserId。第二級簽核人。
    /// </summary>
    public int? SectionChiefUserId { get; set; }

    /// <summary>
    /// 此班期的分署長 UserId。第三級簽核人。
    /// </summary>
    public int? BranchDirectorUserId { get; set; }

    // ─── 導覽屬性 ───

    /// <summary>
    /// 此班期的所有學員。
    /// </summary>
    public ICollection<UserAccount> Students { get; set; } = new List<UserAccount>();

    /// <summary>
    /// 此班期的導師。
    /// </summary>
    public UserAccount? Tutor { get; set; }

    /// <summary>
    /// 此班期的科長。
    /// </summary>
    public UserAccount? SectionChief { get; set; }

    /// <summary>
    /// 此班期的分署長。
    /// </summary>
    public UserAccount? BranchDirector { get; set; }
}
