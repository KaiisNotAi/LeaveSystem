using LeaveSystem.Data;
using LeaveSystem.Tools.ScreenshotCapture;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;

// ════════════════════════════════════════════════════════════════════
// 專題報告截圖擷取工具
//
//   dotnet run --project tools/ScreenshotCapture -- --seed       寫入示範資料
//   dotnet run --project tools/ScreenshotCapture -- --capture    擷取截圖
//   dotnet run --project tools/ScreenshotCapture -- --rollback   移除示範資料
//   dotnet run --project tools/ScreenshotCapture -- --all        seed → capture →（保留資料）
//
// 使用系統既有的 Chrome（Playwright Channel = "chrome"），不下載額外瀏覽器。
// ════════════════════════════════════════════════════════════════════

var modes = args.Where(a => a.StartsWith("--")).Select(a => a.ToLowerInvariant()).ToHashSet();
if (modes.Count == 0)
{
    Console.WriteLine("""
        用法：
          --seed       寫入示範資料（冪等，會先清掉舊的示範資料）
          --capture    啟動網站並擷取截圖到 docs/report/assets/screenshots/
          --rollback   移除全部示範資料
          --mocks      只重繪 mocks/ 下的示意圖（不啟動網站）
          --preview <相對目錄> [輸出目錄]
                       整頁渲染 docs 下的 HTML 檢查破版（預設 docs/report → 系統暫存目錄）
          --all        --seed 後接 --capture
        """);
    return 1;
}

var repoRoot = FindRepoRoot();
Console.WriteLine($"[i] 專案根目錄：{repoRoot}");

// --mocks / --preview 不需要資料庫，先處理掉
if (modes.Contains("--mocks"))
{
    await Capturer.RunMocksOnlyAsync(repoRoot);
    return 0;
}

if (modes.Contains("--preview"))
{
    // 用法：--preview docs/report [輸出目錄]
    var positional = args.Where(a => !a.StartsWith("--")).ToArray();
    var relativeDir = positional.ElementAtOrDefault(0) ?? Path.Combine("docs", "report");
    var previewDir = positional.ElementAtOrDefault(1) ?? Path.Combine(Path.GetTempPath(), "leavesystem-doc-preview");

    await Capturer.RenderPreviewAsync(repoRoot, relativeDir, previewDir);
    return 0;
}

var connectionString = new ConfigurationBuilder()
    .SetBasePath(repoRoot)
    .AddJsonFile("appsettings.json")
    .AddJsonFile("appsettings.Development.json", optional: true)
    .Build()
    .GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("找不到連線字串 DefaultConnection");

var options = new DbContextOptionsBuilder<AppDbContext>()
    .UseSqlServer(connectionString)
    .Options;

var doSeed = modes.Contains("--seed") || modes.Contains("--all");
var doCapture = modes.Contains("--capture") || modes.Contains("--all");
var doRollback = modes.Contains("--rollback");

DemoDataIds? ids = null;

if (doSeed)
{
    await using var db = new AppDbContext(options);
    Console.WriteLine("[+] 寫入示範資料…");
    ids = await DemoSeeder.SeedAsync(db);
    Console.WriteLine($"[✓] 示範班期 Id = {ids.CohortId}，待簽核假單 Id = {ids.PendingTutorRequestId}");
}

if (doCapture)
{
    if (ids is null)
    {
        // 單獨執行 --capture 時，從資料庫回推需要的 Id
        await using var db = new AppDbContext(options);
        var cohort = await db.Cohorts.AsNoTracking()
            .FirstOrDefaultAsync(c => c.Name == DemoSeeder.DemoCohortName)
            ?? throw new InvalidOperationException("找不到示範班期，請先執行 --seed。");

        var pending = await db.LeaveRequests.AsNoTracking()
            .Where(r => r.Student.Username == DemoSeeder.Student1
                     && r.Status == LeaveSystem.Models.Enums.LeaveStatus.Pending)
            .OrderBy(r => r.Id)
            .FirstOrDefaultAsync()
            ?? throw new InvalidOperationException("找不到示範待簽核假單，請先執行 --seed。");

        ids = new DemoDataIds { CohortId = cohort.Id, PendingTutorRequestId = pending.Id };
    }

    await Capturer.RunAsync(repoRoot, ids);
}

if (doRollback)
{
    await using var db = new AppDbContext(options);
    Console.WriteLine("[-] 移除示範資料…");
    await DemoSeeder.RollbackAsync(db);
    Console.WriteLine("[✓] 已還原");
}

return 0;

// 從執行目錄往上找到含 LeaveSystem.csproj 的資料夾
static string FindRepoRoot()
{
    var dir = new DirectoryInfo(AppContext.BaseDirectory);
    while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "LeaveSystem.csproj")))
    {
        dir = dir.Parent;
    }

    return dir?.FullName ?? throw new InvalidOperationException("找不到專案根目錄（LeaveSystem.csproj）");
}
