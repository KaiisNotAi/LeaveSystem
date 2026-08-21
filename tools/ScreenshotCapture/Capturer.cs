using System.Diagnostics;
using System.Text.Json;
using LeaveSystem.Services.Email;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Microsoft.Playwright;

namespace LeaveSystem.Tools.ScreenshotCapture;

/// <summary>
/// 啟動網站 → 以示範帳號登入 → 逐頁擷取截圖。
///
/// 截圖尺寸固定 1600×900（16:9），DeviceScaleFactor = 2 讓輸出為 3200×1800，
/// 投影片放大到全螢幕仍然清晰。
/// </summary>
public static class Capturer
{
    /// <summary>避開開發時常用的 5194，截圖流程自己起一個實例。</summary>
    private const string BaseUrl = "http://localhost:5199";

    private const int ViewportWidth = 1600;
    private const int ViewportHeight = 900;

    public static async Task RunAsync(string repoRoot, DemoDataIds ids)
    {
        var outDir = Path.Combine(repoRoot, "docs", "report", "assets", "screenshots");
        Directory.CreateDirectory(outDir);

        Console.WriteLine($"[i] 截圖輸出目錄：{outDir}");

        using var web = StartWebApp(repoRoot);
        try
        {
            await WaitForServerAsync();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new()
            {
                Channel = "chrome",     // 使用系統已安裝的 Chrome，不下載 Chromium
                Headless = true
            });

            await CaptureAnonymousAsync(browser, outDir);
            await CaptureStudentAsync(browser, outDir);
            await CaptureTutorAsync(browser, outDir, ids);
            await CaptureStaffAsync(browser, outDir);
            await CaptureAdminAsync(browser, outDir, ids);
            await CapturePaperFormMockAsync(browser, repoRoot, outDir);
            await CaptureMailPickupMockAsync(browser, repoRoot, outDir);

            Console.WriteLine("[✓] 全部截圖完成");
        }
        finally
        {
            StopWebApp(web);
        }
    }

    /// <summary>
    /// 只重繪 mocks/ 下的示意圖（不需要啟動網站，調版面時可快速重跑）。
    /// </summary>
    public static async Task RunMocksOnlyAsync(string repoRoot)
    {
        var outDir = Path.Combine(repoRoot, "docs", "report", "assets", "screenshots");
        Directory.CreateDirectory(outDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
        await CapturePaperFormMockAsync(browser, repoRoot, outDir);
        await CaptureMailPickupMockAsync(browser, repoRoot, outDir);
    }

    /// <summary>
    /// 把 docs/ 底下的 HTML（報告投影片、教學站）整頁渲染出來，用來檢查有沒有破版。
    /// 輸出到暫存目錄，不會進版控。
    /// </summary>
    public static async Task RenderPreviewAsync(string repoRoot, string relativeDir, string previewDir)
    {
        var sourceDir = Path.Combine(repoRoot, relativeDir);
        if (!Directory.Exists(sourceDir))
        {
            Console.WriteLine($"[!] 找不到目錄 {sourceDir}");
            return;
        }

        Directory.CreateDirectory(previewDir);

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new() { Channel = "chrome", Headless = true });
        await using var ctx = await NewContextAsync(browser);
        var page = await ctx.NewPageAsync();

        foreach (var file in Directory.GetFiles(sourceDir, "*.html").OrderBy(f => f))
        {
            await page.GotoAsync(new Uri(file).AbsoluteUri);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            var name = Path.GetFileNameWithoutExtension(file);

            // 逐張投影片單獨截圖，才看得出哪一張被 overflow:hidden 裁掉
            var slides = page.Locator("section.slide");
            var count = await slides.CountAsync();
            if (count == 0)
            {
                var whole = Path.Combine(previewDir, $"{name}.png");
                await page.ScreenshotAsync(new() { Path = whole, FullPage = true });

                // 教學站：循序圖是最容易畫歪的部分，額外單獨截一張方便檢查
                var panel = page.Locator(".diagram-panel").First;
                if (await panel.CountAsync() > 0)
                {
                    await panel.ScreenshotAsync(new() { Path = Path.Combine(previewDir, $"{name}-diagram.png") });
                }

                Console.WriteLine($"[✓] {whole}");
                continue;
            }

            for (var i = 0; i < count; i++)
            {
                var outFile = Path.Combine(previewDir, $"{name}-slide{i + 1:00}.png");
                await slides.Nth(i).ScrollIntoViewIfNeededAsync();
                await slides.Nth(i).ScreenshotAsync(new() { Path = outFile });
            }

            Console.WriteLine($"[✓] {name}：{count} 張投影片");
        }
    }

    // ─────────────────────────────────────────────────────────────
    // 各角色的截圖批次
    // ─────────────────────────────────────────────────────────────

    private static async Task CaptureAnonymousAsync(IBrowser browser, string outDir)
    {
        await using var ctx = await NewContextAsync(browser);
        var page = await ctx.NewPageAsync();

        await ShotAsync(page, outDir, "login", "/Account/Login");

        // first-login：預設管理員帳密已填入、尚未送出的狀態
        await page.GotoAsync(BaseUrl + "/Account/Login");
        await page.FillAsync("#Username", "Admin");
        await page.FillAsync("#Password", "Admin@123");
        await SaveAsync(page, outDir, "first-login");
    }

    private static async Task CaptureStudentAsync(IBrowser browser, string outDir)
    {
        await using var ctx = await NewContextAsync(browser);
        var page = await LoginAsync(ctx, DemoSeeder.Student1);

        // HomeController.Index 掛了 [Authorize]，未登入會被導去登入頁，
        // 因此首頁一定要在登入後擷取。
        await ShotAsync(page, outDir, "home-page", "/");
        await ShotAsync(page, outDir, "leave-my", "/LeaveRequests");
        await ShotAsync(page, outDir, "reports-index", "/Reports");
        await ShotAsync(page, outDir, "reports-summary", "/Reports/Summary");
        await ShotAsync(page, outDir, "absence-my", "/AbsenceRecords");
        await ShotAsync(page, outDir, "notification-list", "/Notifications");

        // leave-create：表單先填好，畫面才有內容可看
        await page.GotoAsync(BaseUrl + "/LeaveRequests/Create");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.SelectOptionAsync("#LeaveTypeId", new SelectOptionValue { Label = "病假" });
        await page.FillAsync("#StartDate", "2026-09-15");
        await page.SelectOptionAsync("#StartHour", "8");
        await page.FillAsync("#EndDate", "2026-09-15");
        await page.SelectOptionAsync("#EndHour", "12");
        await page.FillAsync("#Reason", "因流感發燒需在家休養半日，已預約上午門診。");
        await SaveAsync(page, outDir, "leave-create");

        // notification-badge：只截導覽列，讓 🔔 未讀徽章成為主角
        await page.GotoAsync(BaseUrl + "/LeaveRequests");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        var header = page.Locator("header").First;
        await header.ScreenshotAsync(new() { Path = Path.Combine(outDir, "notification-badge.png") });
        Console.WriteLine("[✓] notification-badge.png（元素截圖：導覽列）");
    }

    private static async Task CaptureTutorAsync(IBrowser browser, string outDir, DemoDataIds ids)
    {
        await using var ctx = await NewContextAsync(browser);
        var page = await LoginAsync(ctx, DemoSeeder.Tutor);

        await ShotAsync(page, outDir, "approvals-index", "/Approvals");
        await ShotAsync(page, outDir, "approval-details", $"/Approvals/Details/{ids.PendingTutorRequestId}");
    }

    private static async Task CaptureStaffAsync(IBrowser browser, string outDir)
    {
        await using var ctx = await NewContextAsync(browser);
        var page = await LoginAsync(ctx, DemoSeeder.Staff);

        await ShotAsync(page, outDir, "reports-admin", "/Reports/Admin");
        await ShotAsync(page, outDir, "absence-admin", "/Admin/AbsenceRecords");
        await ShotAsync(page, outDir, "export-page", "/Admin/Export");
    }

    private static async Task CaptureAdminAsync(IBrowser browser, string outDir, DemoDataIds ids)
    {
        await using var ctx = await NewContextAsync(browser);
        var page = await LoginAsync(ctx, DemoSeeder.Admin);

        await ShotAsync(page, outDir, "user-list", "/Admin/Users");
        await ShotAsync(page, outDir, "leave-type-list", "/Admin/LeaveTypes");
        await ShotAsync(page, outDir, "approval-rule", "/Admin/ApprovalRules");
        await ShotAsync(page, outDir, "cohort-edit", $"/Admin/Cohorts/Edit/{ids.CohortId}");
    }

    /// <summary>
    /// paper-form 不是系統畫面，而是「導入系統前的紙本／LINE 通報」示意圖，
    /// 以本機 HTML 模擬後截圖（純示意，不使用任何真實單位名稱或表單格式）。
    /// </summary>
    private static async Task CapturePaperFormMockAsync(IBrowser browser, string repoRoot, string outDir)
    {
        var mock = Path.Combine(repoRoot, "tools", "ScreenshotCapture", "mocks", "paper-form.html");
        if (!File.Exists(mock))
        {
            Console.WriteLine($"[!] 找不到 {mock}，略過 paper-form");
            return;
        }

        await using var ctx = await NewContextAsync(browser);
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(new Uri(mock).AbsoluteUri);
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await SaveAsync(page, outDir, "paper-form");
    }

    /// <summary>
    /// mail-pickup 示意圖：證明「開發期不實寄」時信件確實有被完整產生。
    ///
    /// 畫面上的檔名與 .eml 原文<b>不是編造的</b>，而是當場呼叫網站專案裡真正的
    /// <see cref="MailKitEmailSender"/>（Mode = PickupDirectory）產生到暫存目錄後讀回來的，
    /// 主旨／內文也沿用 NotificationDispatcher 四個事件的實際文案。
    /// 截完圖就把暫存目錄刪掉，不會留下任何檔案。
    /// </summary>
    private static async Task CaptureMailPickupMockAsync(IBrowser browser, string repoRoot, string outDir)
    {
        var mock = Path.Combine(repoRoot, "tools", "ScreenshotCapture", "mocks", "mail-pickup-eml.html");
        if (!File.Exists(mock))
        {
            Console.WriteLine($"[!] 找不到 {mock}，略過 mail-pickup-eml");
            return;
        }

        var tempDir = Path.Combine(Path.GetTempPath(), "leavesystem-mailshot-" + Guid.NewGuid().ToString("N"));
        try
        {
            var payload = await BuildMailPickupPayloadAsync(tempDir);

            await using var ctx = await NewContextAsync(browser);
            var page = await ctx.NewPageAsync();
            // 在頁面自己的 script 執行前先塞資料，mock 頁載入時就能直接渲染
            await page.AddInitScriptAsync($"window.__MAIL_PICKUP__ = {JsonSerializer.Serialize(payload)};");
            await page.GotoAsync(new Uri(mock).AbsoluteUri);
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
            await SaveAsync(page, outDir, "mail-pickup-eml");
        }
        finally
        {
            if (Directory.Exists(tempDir)) Directory.Delete(tempDir, recursive: true);
        }
    }

    /// <summary>
    /// 以真正的 MailKitEmailSender 寄出四封「通知信」到暫存目錄，再把產生的檔案讀回來。
    /// </summary>
    private static async Task<MailPickupPayload> BuildMailPickupPayloadAsync(string tempDir)
    {
        var options = Options.Create(new EmailOptions
        {
            Mode = EmailMode.PickupDirectory,
            PickupDirectory = tempDir,
            From = "noreply@leavesystem.local"
        });
        var sender = new MailKitEmailSender(options, NullLogger<MailKitEmailSender>.Instance);

        // 文案與 Services/Notifications/NotificationDispatcher.cs 的四個事件一致
        var mails = new (string To, string Subject, string Body, string Event)[]
        {
            ("tutor@leavesystem.local", "您有新的請假申請待簽核",
                "學員 王小明 送出的請假申請（事假 16h）需要您簽核。",
                "① 學員送單 → 通知第一關簽核人"),
            ("chief@leavesystem.local", "有請假申請進入您這一關待簽核",
                "學員 王小明 送出的請假申請（事假 16h）需要您簽核。",
                "② 逐級通過 → 通知下一關簽核人"),
            ("wang@leavesystem.local", "您的請假申請已核准",
                "您於 2026/08/07 送出的請假申請（事假 16h）已完成所有簽核，狀態：核准。",
                "③ 全部通過 → 通知申請人"),
            ("wang@leavesystem.local", "您的請假申請已駁回",
                "您於 2026/07/17 送出的請假申請已被駁回。駁回原因：事由描述過於簡略且未附證明，請假不予核准。",
                "④ 任一關駁回 → 通知申請人 + 原因")
        };

        foreach (var (to, subject, body, _) in mails)
        {
            if (!await sender.SendAsync(to, subject, body))
            {
                throw new InvalidOperationException($"產生 .eml 失敗：{subject}");
            }
            // 檔名以毫秒為單位，稍微錯開才不會出現一模一樣的時間
            await Task.Delay(120);
        }

        var files = new DirectoryInfo(tempDir).GetFiles("*.eml").OrderBy(f => f.Name).ToArray();

        // 只攤開第 1 封（待簽核）的原文。四封全攤會讓字級被壓到投影機上看不清楚，
        // 「四個事件各產生一封」由左側檔案清單負責交代。
        var shownIndexes = new[] { 0 };
        var shown = shownIndexes.Select(i => new MailPickupMail(
            files[i].Name,
            File.ReadAllText(files[i].FullName),
            mails[i].Subject)).ToArray();

        return new MailPickupPayload(
            Directory: "LeaveSystem/mail-pickup",
            Files: files.Select((f, i) => new MailPickupFile(f.Name, $"{f.Length:N0} 位元組", mails[i].Event)).ToArray(),
            Shown: shown);
    }

    private sealed record MailPickupFile(string Name, string Size, string Event);

    private sealed record MailPickupMail(string Name, string Raw, string DecodedSubject);

    private sealed record MailPickupPayload(
        string Directory,
        MailPickupFile[] Files,
        MailPickupMail[] Shown);

    // ─────────────────────────────────────────────────────────────
    // 共用流程
    // ─────────────────────────────────────────────────────────────

    private static Task<IBrowserContext> NewContextAsync(IBrowser browser)
        => browser.NewContextAsync(new()
        {
            ViewportSize = new ViewportSize { Width = ViewportWidth, Height = ViewportHeight },
            DeviceScaleFactor = 2,
            Locale = "zh-TW",
            TimezoneId = "Asia/Taipei"
        });

    private static async Task<IPage> LoginAsync(IBrowserContext ctx, string username)
    {
        var page = await ctx.NewPageAsync();
        await page.GotoAsync(BaseUrl + "/Account/Login");
        await page.FillAsync("#Username", username);
        await page.FillAsync("#Password", DemoSeeder.DemoPassword);
        await page.ClickAsync("button[type=submit]");
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

        if (page.Url.Contains("/Account/Login", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"{username} 登入失敗，請確認已執行 --seed。");
        }

        return page;
    }

    private static async Task ShotAsync(IPage page, string outDir, string key, string path)
    {
        var response = await page.GotoAsync(BaseUrl + path);
        if (response is not null && !response.Ok)
        {
            Console.WriteLine($"[!] {key}：{path} 回應 {response.Status}");
        }

        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await SaveAsync(page, outDir, key);
    }

    private static async Task SaveAsync(IPage page, string outDir, string key)
    {
        // FullPage = false：維持 16:9 視窗比例，投影片版面才不會忽高忽低
        await page.ScreenshotAsync(new() { Path = Path.Combine(outDir, $"{key}.png"), FullPage = false });
        Console.WriteLine($"[✓] {key}.png");
    }

    // ─────────────────────────────────────────────────────────────
    // 網站程序生命週期
    // ─────────────────────────────────────────────────────────────

    private static Process StartWebApp(string repoRoot)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            // --no-build：本工具已相依網站專案，執行到這裡時網站一定編好了
            Arguments = $"run --project \"{Path.Combine(repoRoot, "LeaveSystem.csproj")}\" --no-build --no-launch-profile",
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        psi.Environment["ASPNETCORE_ENVIRONMENT"] = "Development";
        psi.Environment["ASPNETCORE_URLS"] = BaseUrl;

        Console.WriteLine($"[i] 啟動網站於 {BaseUrl} …");
        var process = Process.Start(psi) ?? throw new InvalidOperationException("無法啟動網站程序");

        process.OutputDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"   [web] {e.Data}"); };
        process.ErrorDataReceived += (_, e) => { if (e.Data is not null) Console.WriteLine($"   [web!] {e.Data}"); };
        process.BeginOutputReadLine();
        process.BeginErrorReadLine();

        return process;
    }

    private static async Task WaitForServerAsync()
    {
        using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(5) };
        var deadline = DateTime.UtcNow.AddSeconds(90);

        while (DateTime.UtcNow < deadline)
        {
            try
            {
                var res = await http.GetAsync(BaseUrl + "/");
                if (res.IsSuccessStatusCode)
                {
                    Console.WriteLine("[✓] 網站已就緒");
                    return;
                }
            }
            catch (HttpRequestException) { /* 還沒起來，繼續等 */ }
            catch (TaskCanceledException) { /* 逾時，繼續等 */ }

            await Task.Delay(500);
        }

        throw new TimeoutException($"等待 {BaseUrl} 逾時（90 秒）");
    }

    private static void StopWebApp(Process process)
    {
        try
        {
            if (!process.HasExited)
            {
                Console.WriteLine("[i] 關閉網站程序…");
                process.Kill(entireProcessTree: true);
                process.WaitForExit(10_000);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[!] 關閉網站程序時發生例外：{ex.Message}");
        }
    }
}
