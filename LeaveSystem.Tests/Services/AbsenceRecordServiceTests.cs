using LeaveSystem.Models.ViewModels;
using LeaveSystem.Services;
using LeaveSystem.Tests.Helpers;
using Microsoft.EntityFrameworkCore;

namespace LeaveSystem.Tests.Services;

/// <summary>
/// Phase 6 曠課紀錄服務的核心業務規則測試。
///
/// TDD 節奏：
///   Red   ← 先寫此處測試（此時 AbsenceRecordService 全丟 NotImplementedException）
///   Green ← 接著補 AbsenceRecordService 實作，讓每一條測試逐一變綠
///   Refactor ← 收尾整理程式碼，測試仍需全綠
///
/// 對應規格：<c>docs/01-需求說明.md §3.5 / §4</c>、<c>Models/Entities/AbsenceRecord.cs</c>
/// 測試資料：<see cref="AbsenceRecordTestScenario"/>（2 班期 × 2 學員 + 4 筆已存在紀錄）
/// </summary>
public class AbsenceRecordServiceTests
{
    // ═════════════════════════════════════════════════════════════════
    // 群組 1：Create — 新增紀錄的商業規則
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Create_合法輸入_應成功寫入並設定稽核欄位()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordCreateInput
        {
            StudentId = AbsenceRecordTestScenario.StudentUserIdA,
            OccurredAt = new DateTime(2026, 9, 1, 8, 0, 0),
            Hours = 3m,
            Note = "  上午請假但未到  "  // 順便驗 Trim
        };
        var before = DateTime.UtcNow.AddSeconds(-1);

        var result = await sut.CreateAsync(input, createdByUserId: AbsenceRecordTestScenario.StaffUserId);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.NotNull(result.RecordId);

        var saved = await scenario.Db.AbsenceRecords.AsNoTracking()
            .FirstAsync(r => r.Id == result.RecordId);
        Assert.Equal(AbsenceRecordTestScenario.StudentUserIdA, saved.StudentId);
        Assert.Equal(new DateTime(2026, 9, 1, 8, 0, 0), saved.OccurredAt);
        Assert.Equal(3m, saved.Hours);
        Assert.Equal("上午請假但未到", saved.Note); // 已 Trim
        Assert.Equal(AbsenceRecordTestScenario.StaffUserId, saved.CreatedByUserId);
        Assert.InRange(saved.CreatedAt, before, DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task Create_學員Id不存在_應回傳失敗()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordCreateInput
        {
            StudentId = 999_999,  // 不存在的 UserId
            OccurredAt = new DateTime(2026, 9, 1, 8, 0, 0),
            Hours = 2m
        };

        var result = await sut.CreateAsync(input, createdByUserId: AbsenceRecordTestScenario.StaffUserId);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Null(result.RecordId);
    }

    [Fact]
    public async Task Create_非Student角色的UserId_應回傳失敗()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordCreateInput
        {
            StudentId = AbsenceRecordTestScenario.OutsiderUserId,  // 存在但無 Student 角色
            OccurredAt = new DateTime(2026, 9, 1, 8, 0, 0),
            Hours = 2m
        };

        var result = await sut.CreateAsync(input, createdByUserId: AbsenceRecordTestScenario.StaffUserId);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    [Fact]
    public async Task Create_Note全空白_應寫入Null()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordCreateInput
        {
            StudentId = AbsenceRecordTestScenario.StudentUserIdA,
            OccurredAt = new DateTime(2026, 9, 1, 8, 0, 0),
            Hours = 1m,
            Note = "   "
        };

        var result = await sut.CreateAsync(input, createdByUserId: AbsenceRecordTestScenario.StaffUserId);

        Assert.True(result.Success, result.ErrorMessage);
        var saved = await scenario.Db.AbsenceRecords.AsNoTracking()
            .FirstAsync(r => r.Id == result.RecordId);
        Assert.Null(saved.Note);
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 2：Update — 編輯紀錄
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Update_合法輸入_應更新欄位且不變動稽核欄位()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        // 記下原稽核欄位
        var original = await scenario.Db.AbsenceRecords.AsNoTracking()
            .FirstAsync(r => r.Id == scenario.RecordAId1);
        var originalCreatedAt = original.CreatedAt;
        var originalCreatedBy = original.CreatedByUserId;

        var input = new AbsenceRecordEditInput
        {
            Id = scenario.RecordAId1,
            StudentId = AbsenceRecordTestScenario.StudentUserIdB,  // 改成另一位學員
            OccurredAt = new DateTime(2026, 6, 8, 9, 30, 0),
            Hours = 5m,
            Note = "改為半天曠課"
        };

        var result = await sut.UpdateAsync(input);

        Assert.True(result.Success, result.ErrorMessage);
        var updated = await scenario.Db.AbsenceRecords.AsNoTracking()
            .FirstAsync(r => r.Id == scenario.RecordAId1);
        Assert.Equal(AbsenceRecordTestScenario.StudentUserIdB, updated.StudentId);
        Assert.Equal(5m, updated.Hours);
        Assert.Equal("改為半天曠課", updated.Note);

        // 稽核欄位保留原值
        Assert.Equal(originalCreatedAt, updated.CreatedAt);
        Assert.Equal(originalCreatedBy, updated.CreatedByUserId);
    }

    [Fact]
    public async Task Update_紀錄不存在_應回傳失敗()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordEditInput
        {
            Id = 999_999,
            StudentId = AbsenceRecordTestScenario.StudentUserIdA,
            OccurredAt = DateTime.Now,
            Hours = 1m
        };

        var result = await sut.UpdateAsync(input);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 3：Delete — 刪除紀錄
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Delete_存在_應成功刪除()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var result = await sut.DeleteAsync(scenario.RecordAId3);

        Assert.True(result.Success, result.ErrorMessage);
        var exists = await scenario.Db.AbsenceRecords.AsNoTracking()
            .AnyAsync(r => r.Id == scenario.RecordAId3);
        Assert.False(exists);
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 4：GetForAdmin — 行政清單查詢
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetForAdmin_無篩選_應回傳全部並計算總時數()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var vm = await sut.GetForAdminAsync(new AbsenceRecordAdminQuery());

        // 4 筆全回，總時數 = 2 + 4 + 1 + 8 = 15
        Assert.Equal(4, vm.Items.Count);
        Assert.Equal(15m, vm.TotalHours);

        // 排序：OccurredAt DESC（最新在最上）→ B1(6/20) 應在 A2(6/15) 前面，A3(7/10) 應在最上
        Assert.Equal(new DateTime(2026, 7, 10, 8, 0, 0), vm.Items[0].OccurredAt);

        // 下拉選項有填
        Assert.NotEmpty(vm.CohortOptions);
        Assert.NotEmpty(vm.StudentOptions);

        // ListItem 帶學員/班期/登錄者顯示名
        var latest = vm.Items[0];
        Assert.Equal("小明", latest.StudentDisplayName);
        Assert.Equal("A班-2026春", latest.CohortName);
        Assert.Equal("行政小姐", latest.CreatedByDisplayName);
    }

    [Fact]
    public async Task GetForAdmin_依CohortId篩選_應只回該班學員的紀錄()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var vm = await sut.GetForAdminAsync(new AbsenceRecordAdminQuery
        {
            CohortId = AbsenceRecordTestScenario.CohortIdA
        });

        // A 班：A1 + A2 + A3 = 7h（3 筆）
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(7m, vm.TotalHours);
        Assert.All(vm.Items, i => Assert.Equal("A班-2026春", i.CohortName));
    }

    [Fact]
    public async Task GetForAdmin_依StudentId篩選_應只回該學員的紀錄()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var vm = await sut.GetForAdminAsync(new AbsenceRecordAdminQuery
        {
            StudentId = AbsenceRecordTestScenario.StudentUserIdB
        });

        Assert.Single(vm.Items);
        Assert.Equal(8m, vm.TotalHours);
        Assert.Equal("小華", vm.Items[0].StudentDisplayName);
    }

    [Fact]
    public async Task GetForAdmin_依日期區間篩選_DateTo應含當天()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        // 模擬 <input type="date"> 送出 6/1 ~ 6/30，時間都是 00:00:00
        // Service 需自動把 DateTo 補到當日 23:59:59，否則 6/20 08:00 的 B1 會被切掉
        var vm = await sut.GetForAdminAsync(new AbsenceRecordAdminQuery
        {
            DateFrom = new DateTime(2026, 6, 1),
            DateTo = new DateTime(2026, 6, 30)
        });

        // 6 月：A1 + A2 + B1 = 14h（3 筆），A3(7/10) 不算
        Assert.Equal(3, vm.Items.Count);
        Assert.Equal(14m, vm.TotalHours);
        Assert.All(vm.Items, i => Assert.Equal(6, i.OccurredAt.Month));
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 5：GetMine — 學員自查
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetMine_應只回自己的紀錄且TotalHours為終身總計()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        // 學員 A 帶日期篩選（只挑 7 月）
        var vm = await sut.GetMineAsync(
            AbsenceRecordTestScenario.StudentUserIdA,
            new MyAbsenceRecordQuery
            {
                DateFrom = new DateTime(2026, 7, 1),
                DateTo = new DateTime(2026, 7, 31)
            });

        // 7 月只有 A3 一筆
        Assert.Single(vm.Items);
        Assert.Equal(1m, vm.Items[0].Hours);

        // 關鍵斷言：TotalHours **不受篩選影響**，是學員 A 的終身總計 = 7h
        Assert.Equal(7m, vm.TotalHours);

        // 額外驗證：即使學員 B 的資料存在，也不會被撈到
        var vmB = await sut.GetMineAsync(
            AbsenceRecordTestScenario.StudentUserIdB,
            new MyAbsenceRecordQuery());
        Assert.Single(vmB.Items);
        Assert.Equal(8m, vmB.TotalHours);
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 6：GetForEdit — 載入編輯表單
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task GetForEdit_存在_應回傳含表單資料與下拉選項()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = await sut.GetForEditAsync(scenario.RecordAId1);

        Assert.NotNull(input);
        Assert.Equal(scenario.RecordAId1, input!.Id);
        Assert.Equal(AbsenceRecordTestScenario.StudentUserIdA, input.StudentId);
        Assert.Equal(2m, input.Hours);
        Assert.Equal("遲到過早", input.Note);
        Assert.NotEmpty(input.StudentOptions);
    }

    [Fact]
    public async Task GetForEdit_不存在_應回傳Null()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = await sut.GetForEditAsync(999_999);

        Assert.Null(input);
    }

    // ═════════════════════════════════════════════════════════════════
    // 群組 7：兩層下拉相關規則（Phase 6 UX 優化）
    //   規則:
    //     ‧ 未指派班期的學員不可被登錄曠課(UI 擋 + Service 擋)
    //     ‧ GetStudentOptionsAsync 也預先過濾未指派班期
    //     ‧ 新增公開 GetCohortOptionsAsync 供表單「班期」下拉使用
    // ═════════════════════════════════════════════════════════════════

    [Fact]
    public async Task Create_未指派班期的Student_應回傳失敗()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var input = new AbsenceRecordCreateInput
        {
            StudentId = AbsenceRecordTestScenario.UnassignedStudentUserId,  // 有 Student 角色但 CohortId=null
            OccurredAt = new DateTime(2026, 9, 1, 8, 0, 0),
            Hours = 2m
        };

        var result = await sut.CreateAsync(input, createdByUserId: AbsenceRecordTestScenario.StaffUserId);

        Assert.False(result.Success);
        Assert.False(string.IsNullOrWhiteSpace(result.ErrorMessage));
        Assert.Null(result.RecordId);
    }

    [Fact]
    public async Task GetStudentOptionsAsync_應排除未指派班期的學員_並回填CohortId()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var options = await sut.GetStudentOptionsAsync();

        // 只該有小明、小華，沒有小陳(未指派班期)
        Assert.Equal(2, options.Count);
        Assert.Contains(options, o => o.Id == AbsenceRecordTestScenario.StudentUserIdA);
        Assert.Contains(options, o => o.Id == AbsenceRecordTestScenario.StudentUserIdB);
        Assert.DoesNotContain(options, o => o.Id == AbsenceRecordTestScenario.UnassignedStudentUserId);

        // 每一個 option 都要帶 CohortId,前端才能依此過濾學員下拉
        var optionA = options.Single(o => o.Id == AbsenceRecordTestScenario.StudentUserIdA);
        var optionB = options.Single(o => o.Id == AbsenceRecordTestScenario.StudentUserIdB);
        Assert.Equal(AbsenceRecordTestScenario.CohortIdA, optionA.CohortId);
        Assert.Equal(AbsenceRecordTestScenario.CohortIdB, optionB.CohortId);
    }

    [Fact]
    public async Task GetCohortOptionsAsync_應回傳全部班期依名稱排序()
    {
        using var scenario = await AbsenceRecordTestScenario.CreateAsync();
        var sut = new AbsenceRecordService(scenario.Db);

        var options = await sut.GetCohortOptionsAsync();

        Assert.Equal(2, options.Count);
        // 名稱升序:「A班-2026春」在前,「B班-2026春」在後
        Assert.Equal(AbsenceRecordTestScenario.CohortIdA, options[0].Id);
        Assert.Equal(AbsenceRecordTestScenario.CohortIdB, options[1].Id);
        Assert.Equal("A班-2026春", options[0].Name);
        Assert.Equal("B班-2026春", options[1].Name);
    }
}
