using System.ComponentModel.DataAnnotations;
using System.Text.Json;

namespace LeaveSystem.Models.ViewModels;

/// <summary>
/// 簽核角色候選（僅這三個為簽核鏈使用；順序即簽核順序）。
/// </summary>
public static class ApprovalRoleCatalog
{
    public static readonly (string Value, string DisplayName)[] All =
    {
        ("Tutor", "導師"),
        ("SectionChief", "科長"),
        ("BranchDirector", "分署長")
    };

    /// <summary>
    /// 依候選順序輸出字串（例：Tutor,SectionChief）供 UI 呈現。
    /// </summary>
    public static string ToDisplay(IEnumerable<string> roles)
    {
        var map = All.ToDictionary(x => x.Value, x => x.DisplayName);
        var ordered = All
            .Where(x => roles.Contains(x.Value))
            .Select(x => map[x.Value]);
        return string.Join(" → ", ordered);
    }
}

/// <summary>
/// 行政後台「建立簽核規則」表單。
/// </summary>
public class ApprovalRuleCreateViewModel
{
    [Required]
    [Range(0, 1000, ErrorMessage = "區間下限需 0 ~ 1000")]
    [Display(Name = "區間下限（含，小時）")]
    public decimal MinHours { get; set; } = 0;

    [Range(0, 1000, ErrorMessage = "區間上限需 0 ~ 1000（留空代表無上限）")]
    [Display(Name = "區間上限（含，小時；留空代表無上限）")]
    public decimal? MaxHours { get; set; }

    [Required(ErrorMessage = "請至少選擇一個簽核角色")]
    [Display(Name = "簽核角色（依順序簽核）")]
    public List<string> SelectedRoles { get; set; } = new();

    [StringLength(200)]
    [Display(Name = "描述")]
    public string? Description { get; set; }
}

/// <summary>
/// 行政後台「編輯簽核規則」表單。
/// </summary>
public class ApprovalRuleEditViewModel
{
    public int Id { get; set; }

    [Required]
    [Range(0, 1000)]
    [Display(Name = "區間下限（含，小時）")]
    public decimal MinHours { get; set; }

    [Range(0, 1000)]
    [Display(Name = "區間上限（含，小時；留空代表無上限）")]
    public decimal? MaxHours { get; set; }

    [Required(ErrorMessage = "請至少選擇一個簽核角色")]
    [Display(Name = "簽核角色（依順序簽核）")]
    public List<string> SelectedRoles { get; set; } = new();

    [StringLength(200)]
    [Display(Name = "描述")]
    public string? Description { get; set; }
}

/// <summary>
/// RequiredRoles 欄位（JSON 字串）與 List&lt;string&gt; 的互轉工具。
/// </summary>
public static class ApprovalRoleJson
{
    /// <summary>
    /// 從資料庫的 JSON 字串轉回排序後的 List。
    /// </summary>
    public static List<string> Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return new();
        try
        {
            var arr = JsonSerializer.Deserialize<List<string>>(json);
            if (arr is null) return new();
            // 依 Catalog 中的順序重排（避免資料庫存的順序亂）
            var order = ApprovalRoleCatalog.All.Select(x => x.Value).ToList();
            return arr.Where(order.Contains).OrderBy(r => order.IndexOf(r)).ToList();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// 從 UI 收到的 List 轉成資料庫要存的 JSON 字串（依 Catalog 順序）。
    /// </summary>
    public static string Serialize(IEnumerable<string> roles)
    {
        var order = ApprovalRoleCatalog.All.Select(x => x.Value).ToList();
        var ordered = roles.Where(order.Contains).OrderBy(r => order.IndexOf(r)).ToList();
        return JsonSerializer.Serialize(ordered);
    }
}
