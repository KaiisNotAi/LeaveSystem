# Email 通知設定說明（Phase 7）

> 對應程式：`Services/Email/*`、`Services/Notifications/NotificationDispatcher.cs`、`appsettings*.json` 的 `Email` 區段。

## 目前狀態（開發階段）

**Mode = `PickupDirectory`（不會真的寄信）**

- 每次觸發通知時，`MailKitEmailSender` 會把郵件寫成 `.eml` 檔到 `./mail-pickup/` 目錄，**不連任何 SMTP 伺服器**。
- 可以用 Outlook / Thunderbird 直接開 `.eml` 檔預覽長相，或用文字編輯器看內容。
- 站內通知（🔔 徽章 + `/Notifications` 列表）**仍會正常運作**，因為那是寫進 DB，不需要 SMTP。

這樣的好處：
- 開發／測試時**不會誤寄信給真人**。
- 不需要準備任何 SMTP 帳號、憑證。
- 也不會因為外部服務故障而影響簽核流程測試。

## 相關角色（別搞混）

| 角色 | 是誰 | 存在哪 |
|---|---|---|
| **寄件者帳號**（SMTP 帳號密碼） | 系統的「信差」，讓程式登入某個郵件伺服器代為發信 | `appsettings.json` 的 `Email:Smtp` 區段 |
| **收件者信箱** | 系統的使用者（學員／導師…） | 資料庫 `UserAccount.Email` 欄位 |

程式本身不能憑空送信，必須登入某台郵件伺服器（Gmail / Outlook / 公司內部 SMTP…）請它幫忙寄。就像人要收發信也要先設定一組帳號登入一樣。

## 未來要真的寄信，切換步驟

### 選項 A：Gmail 當寄件者（最簡單，適合小量測試）

1. 該 Gmail 帳號開啟「兩步驟驗證」。
2. Google 帳戶 → 安全性 → **應用程式密碼**，建立一組 16 碼專用密碼（不是你的 Gmail 登入密碼）。
3. 修改 `appsettings.json`（或用 User Secrets）：

```json
"Email": {
  "Mode": "Smtp",
  "From": "your-account@gmail.com",
  "Smtp": {
	"Host": "smtp.gmail.com",
	"Port": 587,
	"UseSsl": true,
	"Username": "your-account@gmail.com",
	"Password": "剛建立的 16 碼應用程式密碼"
  }
}
```

4. 讓每位使用者的 `UserAccount.Email` 有值（後台管理頁維護），否則該人只有站內通知、不會收 email。

### 選項 B：公司／學校內部 SMTP

通常由 IT 提供主機、port、（可能免帳密）。填進對應欄位即可。

### 選項 C：專業寄信服務（正式上線建議）

如 SendGrid、Mailgun、Amazon SES。它們給你一組 SMTP 帳密或 API Key，寄送穩定度與抗擋信能力遠優於個人 Gmail，且免額度內免費。

## 實寄前必須修正的程式缺口

上面「切換步驟」只涵蓋設定。以下三處是**只有真的連 SMTP 才會踩到**的問題，落檔模式完全看不出來，切換前應一併處理。

### 1. 加密模式對應不完整

`Services/Email/MailKitEmailSender.cs` 目前為：

```csharp
var secure = _options.Smtp.UseSsl ? SecureSocketOptions.StartTlsWhenAvailable : SecureSocketOptions.None;
```

兩個問題：

- **465 埠連不上**。465 是「隱式 SSL」（一連上就握手），必須用 `SecureSocketOptions.SslOnConnect`；用 `StartTlsWhenAvailable` 會卡在等待明文問候而逾時。
- **帳密可能以明文送出**。`StartTlsWhenAvailable` 在伺服器沒有宣告 STARTTLS 能力時會**靜默降級成不加密**，接著照樣把 `Username` / `Password` 送出去。

建議改為依 Port 判斷，或把設定從 `bool UseSsl` 改成明確的加密模式列舉：

| Port | 應使用 |
|---|---|
| 465 | `SslOnConnect` |
| 587 | `StartTls`（**Required**，不可 WhenAvailable） |
| 25 | 內網無認證才可用 `None`；有帳密一律要求加密 |

### 2. 信件內文缺少系統連結

`Services/Notifications/NotificationDispatcher.cs` 的 `DispatchAsync` 只把 `title` / `message` 交給 `IEmailSender`，同一方法算出的 `url`（來自 `BuildApprovalUrl` / `BuildRequestUrl`）**只寫進站內通知，沒有進到信件**。收信人得自己回系統翻找對應假單。

要修正需要兩件事：

- 新增一個 BaseUrl 設定（例如 `Email:BaseUrl`），因為 `BuildApprovalUrl` 產生的是 `/Approvals/Details/5` 這種相對路徑，信件裡必須是絕對網址才能點。
- `IEmailSender.SendAsync` 目前只收純文字 body，需擴充為支援 HTML 內文。

### 3. 同步寄送會阻塞使用者請求

`DispatchAsync` 是在簽核的 HTTP 請求執行緒內 `await` 整套 SMTP 連線 → 認證 → 寄送 → 斷線。落檔模式下這只是寫本機檔案，感覺不出來；改走 SMTP 後每封信約需 1～3 秒，按下「核准」的人會明顯感覺到停頓，且一次要通知多人時會累加。

建議改為背景寄送（`System.Threading.Channels` 佇列 + `BackgroundService` 消化），請求端只負責入列。注意背景服務是 Singleton，不能直接注入 Scoped 的 `AppDbContext` / `IEmailSender`，需自行建立 scope。

## 驗證順序（別一開始就接真信箱）

1. **先接本機假 SMTP**：安裝 Papercut-SMTP / MailHog / smtp4dev，監聽 `localhost:25`，設定 `Mode=Smtp`、`Host=localhost`、`Port=25`、免帳密。
   這一步確認的是「程式真的走了 SMTP 分支且信件格式正確」，而且信一封都出不了本機。
2. **再換真實帳號**，先只寄給自己的信箱測試。
3. **非正式環境加防誤寄開關**：建議增加類似 `Email:RedirectAllTo` 的設定，只要有值就把所有收件人強制改成該信箱，並在主旨標注原收件人。這樣即使拿正式資料庫做測試也不會騷擾到真人。

## 安全提醒（切換到 Smtp 前必看）

- **不要把 SMTP 密碼直接 commit 進 `appsettings.json`**。可用：
  - Dev：`dotnet user-secrets set "Email:Smtp:Password" "xxx"`
  - Prod：環境變數（`Email__Smtp__Password`）或 Azure Key Vault
- 目前實作為**同步寄送 + try/catch**：失敗只記 log 不阻斷簽核流程（見 `NotificationDispatcher.DispatchAsync`）。
- Gmail 不支援直接用登入密碼；Office 365 建議走 OAuth（目前只支援基本認證，若需 OAuth 要擴充 `MailKitEmailSender`）。

## 相關檔案索引

- `appsettings.json` / `appsettings.Development.json` → `Email` 區段
- `Services/Email/EmailOptions.cs` → 設定綁定 class
- `Services/Email/MailKitEmailSender.cs` → 依 `Mode` 分流的寄送實作
- `Services/Notifications/NotificationDispatcher.cs` → 4 事件觸發點（送單／逐級通過／全通過／駁回）
- `LeaveSystem.Tests/Services/EmailSenderTests.cs` → PickupDirectory 落檔驗證
