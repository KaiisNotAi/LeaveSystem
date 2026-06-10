using LeaveSystem.Data;
using Microsoft.EntityFrameworkCore;

// ════════════════════════════════════════════════════════════════════
// 學生請假系統啟動進入點
// ASP.NET Core 應用程式由「服務註冊」與「中介軟體管線」兩段組成。
// ════════════════════════════════════════════════════════════════════

var builder = WebApplication.CreateBuilder(args);

// ─────────────────────────────────────────────────────────────────
// 服務註冊區（DI 容器）
// 在這裡告訴 DI 容器「將來執行時，遇到某型別請給我這個實作」。
// ─────────────────────────────────────────────────────────────────

// 註冊 MVC：Controllers 與 Views
builder.Services.AddControllersWithViews();

// 註冊 EF Core DbContext，使用 SQL Server
// 連線字串從 appsettings.json 的 ConnectionStrings:DefaultConnection 讀取
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("找不到連線字串 DefaultConnection，請檢查 appsettings.json");
    options.UseSqlServer(connectionString);
});

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────
// 中介軟體管線區（HTTP 請求處理流程）
// 進來的 HTTP 請求會依序通過下方註冊的中介軟體，最後交給 Controller。
// 順序非常重要：UseRouting → UseAuthentication → UseAuthorization → MapXxx
// ─────────────────────────────────────────────────────────────────

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseRouting();

app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

