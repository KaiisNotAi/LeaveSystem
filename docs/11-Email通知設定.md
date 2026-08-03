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
