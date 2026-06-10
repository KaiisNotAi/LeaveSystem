using LeaveSystem.Data;
using LeaveSystem.Services;
using Microsoft.AspNetCore.Authentication.Cookies;
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

// 讓行政後台 Controllers/Admin/XxxController 的 View 可以擺在 Views/Admin/Xxx/ 子資料夾
// 預設 Razor 只會找 Views/Xxx/，多加一條搜尋路徑即可同時支援
builder.Services.Configure<Microsoft.AspNetCore.Mvc.Razor.RazorViewEngineOptions>(options =>
{
    options.ViewLocationFormats.Insert(0, "/Views/Admin/{1}/{0}.cshtml");
});

// 註冊 EF Core DbContext，使用 SQL Server
// 連線字串從 appsettings.json 的 ConnectionStrings:DefaultConnection 讀取
builder.Services.AddDbContext<AppDbContext>(options =>
{
    var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
        ?? throw new InvalidOperationException("找不到連線字串 DefaultConnection，請檢查 appsettings.json");
    options.UseSqlServer(connectionString);
});

// 註冊認證服務（自訂帳號 + Cookie 模式，不使用 ASP.NET Core Identity）
builder.Services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
builder.Services.AddScoped<IAuthService, AuthService>();

// 註冊 Cookie 認證
//   LoginPath：未登入造訪需登入頁面時自動轉址到這裡
//   AccessDeniedPath：登入了但角色不足時轉址到這裡
//   ExpireTimeSpan：Cookie 8 小時後過期，需重新登入
builder.Services
    .AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login";
        options.AccessDeniedPath = "/Account/AccessDenied";
        options.ExpireTimeSpan = TimeSpan.FromHours(8);
        options.SlidingExpiration = true;   // 使用中會自動延長到期時間
        options.Cookie.Name = "LeaveSystem.Auth";
        options.Cookie.HttpOnly = true;     // 防止 JS 讀取 Cookie，降低 XSS 風險
    });

var app = builder.Build();

// ─────────────────────────────────────────────────────────────────
// 啟動時自動建立資料庫並寫入種子資料
// 這樣第一次跑就會有預設管理員、角色、假別、簽核規則可用。
// ─────────────────────────────────────────────────────────────────
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();

    // EnsureCreated() 是「快速建表」工具，適合開發階段。
    // 之後建議改用 EF Core Migration（dotnet ef migrations add / database update）
    // 來追蹤每次資料表結構變更，請見 docs/06-開發環境設定.md。
    db.Database.EnsureCreated();

    SeedData.Initialize(db);
}

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

// 一定要在 UseAuthorization 之前；否則沒有「使用者身分」可以授權
app.UseAuthentication();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();

