# Phase 9 評估文件 — LINE 通知（保留 / 暫不實作）

> **本文件目前定位為「選項備忘」而非實作交接**。
>
> 使用者於 2026-XX 決策：LINE Messaging API 免費方案僅每月 200 則主動推播訊息，一個班期的請假送單量很容易超額，付費方案（Light NT$ 199/月起）對本專案性價比偏低，故 **Phase 9 LINE 通知功能暫緩實作**。
>
> 本文件保留供未來重啟時參考，內容含：
> 1. LINE 開發者帳號 / Channel 申請完整步驟（未來要做時直接照抄）
> 2. 當時已與使用者討論定案的 MVP 範圍
> 3. 已設計好的架構與 TDD 測試清單
>
> 若未來要重啟：把本檔頂端這段警語移除、切 `feature/phase9-line-notify` 分支、依「六、TDD 測試清單」開始 TDD。

## 為什麼保留（決策理由）

| 考量 | 說明 |
|---|---|
| 免費額度 | LINE Messaging API 免費方案 **200 則/月**主動推播 |
| 實際用量估算 | 若一班 30 人 × 每人每月 5 次請假 = 150 則（僅計送單通知導師）；加上未來擴充學員端推播（過關/駁回）會直接超額 |
| 付費成本 | Light NT$ 約 199/月（500 則）、Standard 更高；用量成長很快就要升 Standard |
| 現有替代 | Phase 7 已完成「站內通知 + Email」雙通道，已能達成通知目的 |
| 替代方案 | 未來若真要 IM 推播，可評估 **Telegram Bot**（免費且無配額）或 **Discord Webhook** |

## 專案原則（沿用，未來重啟仍適用）

- 語言：zh-TW
- 開發流程：**TDD 優先**（先寫測試 → 再實作）— 出自 `.github/copilot-instructions.md`
- 每個 Phase：開 `feature/phaseN-xxx` 分支 → 完成後 `git merge --no-ff master` + 打 tag
- 目標框架：.NET 10，MVC Controllers + Views
- **不使用**：ASP.NET Core Identity、SignalR、Hangfire；沿用現有 Cookie 認證與 EF Core

## 目前 Git 狀態（Phase 8 收尾快照）

- `master` 上一個 tag：`v0.9-phase8`
- **未來重啟時分支名**：`feature/phase9-line-notify`
- Phase 8 結束時測試 87/87 綠、build 0 error

---

## 一、當時與使用者確認的 MVP 範圍

| 項目 | 決策 |
|---|---|
| 通知事件 | ✅ **只做「學員送單」→ 通知導師（Tutor）LINE**<br>❌ ApprovedNext / FullyApproved / Rejected 保留在站內+Email（不推 LINE） |
| 綁定對象 | ✅ **只做老師端綁定**；學員不需綁 LINE |
| 綁定方式 | ✅ **6 碼驗證碼**（老師產生 → 傳給 Bot 完成綁定） |
| 訊息方向 | 單向推播為主 + **最小 Webhook**（僅處理 `follow` 與綁定用文字訊息） |
| 失敗策略 | ✅ **純 log**，不擲例外、不影響業務流程（比照 Email 通道） |
| Dev 環境 | 用 `NullLineMessenger`（不真的打 API，只寫 log） |

---

## 二、【未來重啟時必讀】LINE 開發者帳號與 Messaging API Channel 申請流程

Phase 9 重啟前**一定要先拿到憑證**，否則寫完程式沒東西可測。以下是操作步驟（實測 2025 年下半年後的 Console 介面）。

### Step 1：註冊 LINE 開發者帳號

1. 用**日常在用的 LINE 帳號**登入 <https://developers.line.biz/console/>
   - ⚠️ 請不要用測試帳號，一旦刪除 Provider 綁定會麻煩
2. 首次登入會要求輸入：
   - **Developer name**（開發者名稱，之後可改）
   - **Email**（收官方公告用）
3. 勾選同意條款 → 點 __Create my account__

### Step 2：建立 Provider（服務提供者）

Provider 是「一組 Channel 的擁有者」，可想成公司/團隊帳號。

1. Console 首頁點 __Create a new provider__
2. Provider name 建議填：`LeaveSystem` 或你的單位名
3. 建立完成後會進入 Provider 儀表板

### Step 3：建立 Messaging API Channel（Bot 本體）

1. 在 Provider 頁面點 __Create a Messaging API channel__
2. 依畫面填寫：
   | 欄位 | 填法 |
   |---|---|
   | Channel type | Messaging API |
   | Provider | 剛才建的那個 |
   | Company / Region of operation | Taiwan |
   | Channel icon | 可放單位 logo（可跳過） |
   | Channel name | `學員請假通知` 之類（會顯示為 Bot 名字） |
   | Channel description | 隨意，例：`學員請假送單通知導師` |
   | Category / Subcategory | 選 `Education` → `School` |
   | Email address | 收 LINE 通知用 |
   | Privacy policy URL / ToS URL | 可跳過 |
3. 勾同意 → __Create__

### Step 4：拿到本專案需要的三把鑰匙

進 Channel 後，切以下分頁抄下這三個值：

| 值 | 位置 | 用途 |
|---|---|---|
| **Channel secret** | __Basic settings__ 分頁 | 驗證 Webhook 簽章（HMAC-SHA256） |
| **Channel access token（long-lived）** | __Messaging API__ 分頁 → __Channel access token__ 區塊 → __Issue__ | 呼叫 Push API 用（Bearer） |
| **Bot basic ID**（`@xxxxxxx`） | __Messaging API__ 分頁最上方 | 產生加好友 QR / 深連結 |

### Step 5：Bot 行為調整（**很重要**，官方預設會擋你）

在 __Messaging API__ 分頁往下拉：

| 設定 | 值 | 理由 |
|---|---|---|
| __Auto-reply messages__ | **Disabled** | 預設會回官方罐頭訊息，會蓋掉我們的驗證碼回覆 |
| __Greeting messages__ | Enabled（可放歡迎詞 + 提示綁定步驟） | 使用者加好友時看到 |
| __Webhook URL__ | 暫時空著（Phase 9 開發到 Webhook 那步再填） | |
| __Use webhook__ | **Enabled**（開發那步再開） | |

點 __Edit__ 按鈕會被導去 __LINE Official Account Manager__ 後台調 Auto-reply（同一個帳號登入即可）。

### Step 6：安全保存憑證

- **絕對不要** commit 到 git
- Dev 端用：`dotnet user-secrets set "Line:ChannelAccessToken" "xxx"`
- Prod 端用：環境變數 or Azure Key Vault
- 若不慎外洩：Console 內可 __Reissue__ 撤銷舊 token

### Step 7：測試帳號自己先加 Bot 好友

- 掃 __Messaging API__ 分頁的 QR code，或用 `https://line.me/R/ti/p/{BotBasicId}`
- 加了之後你才拿得到自己的 `LineUserId`（開發時方便測 Push）

### Step 8（開發到 Webhook 時才做）：對外可連的 HTTPS URL

因為 LINE 只會把 webhook 打到公開 HTTPS：

- **開發時最方便**：Visual Studio 2026 內建 __Dev Tunnels__
  - 選單：__Debug > Dev Tunnels > Create a Tunnel__ → Access = Public → Persistent
  - 產出 `https://xxxx.tw.devtunnels.ms`，把 `/api/line/webhook` 貼回 Console 的 Webhook URL
- **正式**：部署到有 HTTPS 網域的伺服器
- 不要用 `ngrok` 免費版（URL 每次會變）

### 費用注意（⚠️ 本專案暫緩的主因）

- Messaging API 免費方案：每月 **200 則主動推播訊息**
- 學員請假頻率若每月 <200 次，免費夠用；超過需升 Light（NT$ 約 199/月，500 則）或 Standard
- 本專案評估後認為配額易滿、成本效益低，故暫緩

---

## 三、既有專案已就緒的基礎建設（未來重啟時無需重建）

Phase 7 已把通知抽象化，Phase 9 只是**加第三通道**，架構完全沿用。

| 元件 | 位置 | 現有職責 |
|---|---|---|
| `INotificationDispatcher` | `Services/Notifications/INotificationDispatcher.cs` | 4 事件：Submitted / ApprovedNext / FullyApproved / Rejected |
| `NotificationDispatcher.DispatchAsync` | `Services/Notifications/NotificationDispatcher.cs` L106-128 | 目前串「站內通知 + Email」，Phase 9 要在此**追加第三段 LINE 推播** |
| `UserAccount` | `Models/Entities/UserAccount.cs` | Phase 9 要**加 `LineUserId` / `LineNotifyEnabled` 兩欄** |
| `AppDbContext` | `Data/AppDbContext.cs` | Phase 9 要**加 `DbSet<LineBindingCode>`** |
| 認證 | Cookie（不使用 Identity） | 綁定頁走現有 `[Authorize]` 即可 |

---

## 四、Phase 9 交付物清單（未來重啟時的實作範圍）

### 4.1 資料層

**Migration：`AddLineNotify`**

- `UserAccount` 加：
  - `LineUserId` (`string?`, `nvarchar(50)`, index)
  - `LineNotifyEnabled` (`bool`, default `true`)
- 新增 Entity `LineBindingCode`：
  | 欄位 | 型別 | 說明 |
  |---|---|---|
  | Id | int PK | |
  | UserAccountId | int FK → UserAccount | 一人一筆有效碼（唯一 index 加 filter `UsedAt IS NULL`） |
  | Code | `nvarchar(6)` | 6 碼英數 |
  | ExpiresAt | DateTime | 產生後 10 分鐘 |
  | UsedAt | DateTime? | 綁定成功時填入 |
  | CreatedAt | DateTime | |

### 4.2 設定

```jsonc
// appsettings.json
"Line": {
  "Enabled": false,
  "ChannelAccessToken": "",
  "ChannelSecret": "",
  "BotBasicId": "@xxx",
  "BindingCodeTtlMinutes": 10,
  "PushApiBaseUrl": "https://api.line.me"
}
```

- `appsettings.Development.json` 保持 `Enabled: false` → DI 註冊 `NullLineMessenger`
- 正式憑證用 User Secrets：

```powershell
dotnet user-secrets set "Line:Enabled" "true"
dotnet user-secrets set "Line:ChannelAccessToken" "xxx"
dotnet user-secrets set "Line:ChannelSecret" "xxx"
dotnet user-secrets set "Line:BotBasicId" "@xxx"
```

### 4.3 服務層

| 檔案 | 責任 |
|---|---|
| `Services/Line/LineOptions.cs` | 綁 `Line:*` |
| `Services/Line/ILineMessenger.cs` | `Task PushTextAsync(string lineUserId, string text, CancellationToken ct)` |
| `Services/Line/LineMessenger.cs` | `IHttpClientFactory` 呼叫 `POST /v2/bot/message/push`；Bearer Token；失敗擲 `LineApiException` 讓上層 log |
| `Services/Line/NullLineMessenger.cs` | 只寫 `_logger.LogInformation`，for Dev/測試 |
| `Services/Line/ILineBindingService.cs` | `GenerateCodeAsync(userId)` / `TryBindAsync(lineUserId, code)` / `UnbindAsync(userId)` |
| `Services/Line/LineBindingService.cs` | 6 碼英數（避免易混淆字元 `0OIl1`）、10 分鐘 TTL、同一 user 產新碼會失效舊碼、已用不可重用 |
| `Services/Line/LineWebhookSignatureValidator.cs` | HMAC-SHA256(base64) 比對 `X-Line-Signature` |

### 4.4 修改現有 Dispatcher

`NotificationDispatcher.NotifyApproverAtCurrentLevelAsync` 中，`DispatchAsync` 之後判斷：

```csharp
// Phase 9：僅「送單」事件推 LINE；其他事件不推
if (isFirstSubmission
	&& approver.LineNotifyEnabled
	&& !string.IsNullOrWhiteSpace(approver.LineUserId))
{
	try
	{
		await _line.PushTextAsync(approver.LineUserId!, $"{title}\n{message}", cancellationToken);
	}
	catch (Exception ex)
	{
		_logger.LogError(ex, "LINE 推播失敗（UserId={UserId}）", approver.Id);
	}
}
```

⚠️ 建議把 LINE push 抽成 `DispatchAsync` 內的第三段（比照 email 段），並多傳一個 `bool sendLine` 參數，避免其他 3 個事件不小心推播。

### 4.5 Controller / View

- `Controllers/LineWebhookController.cs`
  - `[AllowAnonymous]`、`POST /api/line/webhook`
  - 先驗簽（失敗 400），再解析 events：
	- `follow` → 暫存該 `LineUserId`（可先只 log，靠使用者送驗證碼時直接帶）
	- `message.text` 且長度 6 → 呼叫 `TryBindAsync`，回覆綁定結果訊息（用 Reply API，免計 push 額度）
- `Controllers/AccountController` 加 action：
  - `GET /Account/LineBinding`：顯示狀態、產生按鈕、Bot 加好友連結 / QR
  - `POST /Account/LineBinding/GenerateCode`：呼叫 `GenerateCodeAsync`
  - `POST /Account/LineBinding/Unbind`：解除綁定
- `Views/Account/LineBinding.cshtml`
- `_Layout` 導師/科長/分署長選單加「LINE 綁定」入口

### 4.6 DI 註冊（`Program.cs`）

```csharp
builder.Services.Configure<LineOptions>(builder.Configuration.GetSection("Line"));
builder.Services.AddHttpClient<LineMessenger>();
builder.Services.AddScoped<ILineBindingService, LineBindingService>();
builder.Services.AddSingleton<LineWebhookSignatureValidator>();

if (builder.Configuration.GetValue<bool>("Line:Enabled"))
{
	builder.Services.AddScoped<ILineMessenger, LineMessenger>();
}
else
{
	builder.Services.AddScoped<ILineMessenger, NullLineMessenger>();
}
```

---

## 五、TDD 測試清單（未來重啟時依此先寫測試）

放在 `LeaveSystem.Tests/Services/Line/`：

1. **`LineMessengerTests`**（mock `HttpMessageHandler`）
   - 打對 URL：`https://api.line.me/v2/bot/message/push`
   - Authorization header = `Bearer {token}`
   - Request body JSON：`{ "to": "...", "messages": [ { "type": "text", "text": "..." } ] }`
   - 5xx 時擲 `LineApiException`

2. **`LineWebhookSignatureValidatorTests`**
   - 正確簽章 → true
   - 內容竄改 → false
   - 空簽章 → false

3. **`LineBindingServiceTests`**（用 InMemory 或 SQLite In-Memory DbContext）
   - 產生的碼長度 6、字元集合正確
   - TTL 10 分鐘（過期無法用）
   - 同一 user 產生新碼時舊碼會被失效
   - `TryBindAsync` 成功：`UserAccount.LineUserId` 被寫入、`UsedAt` 有值
   - `TryBindAsync` 失敗：碼錯 / 過期 / 已用 → 回 false
   - `UnbindAsync`：`LineUserId` 清空

4. **`NotificationDispatcherTests` 擴充**
   - 導師有 `LineUserId` + `LineNotifyEnabled=true` + 送單事件 → `ILineMessenger.PushTextAsync` 被呼叫一次
   - 同上條件但事件為 `ApprovedNext` → **不呼叫** LINE
   - 導師無 `LineUserId` → 不呼叫、不擲例外
   - `LineNotifyEnabled=false` → 不呼叫
   - `PushTextAsync` 擲例外 → 站內 + Email 仍執行完畢

5. **`LineWebhookControllerTests`**
   - 簽章錯 → 400
   - `message.text` = 有效碼 → 呼叫 `TryBindAsync`、回 200
   - `follow` 事件 → 200（本 MVP 只 log）

**目標**：Phase 8 結束 87 個 → Phase 9 結束預估 **105-110 個**測試全綠。

---

## 六、未來重啟時的驗收 checklist

- [ ] 老師登入後可在 `/Account/LineBinding` 產生 6 碼驗證碼
- [ ] 老師加 Bot 好友並傳送驗證碼，Bot 回覆綁定成功
- [ ] 資料庫 `UserAccount.LineUserId` 有值
- [ ] 學員送出請假 → 對應導師的 LINE 收到訊息（含學員名/假別/時數）
- [ ] 其他 3 個事件（過關/全通過/駁回）**不推** LINE
- [ ] LINE API 掛掉時：業務流程仍完成、站內通知仍寫入、log 有錯誤訊息
- [ ] `Line:Enabled=false` 時系統正常運作（`NullLineMessenger`）
- [ ] Webhook 簽章驗證正確拒絕竄改請求
- [ ] `dotnet build` 0 error、測試全綠
- [ ] `docs/05-開發階段與里程碑.md` Phase 9 段落狀態更新為完成

---

## 七、需澄清問題（未來重啟時開場先問使用者）

1. **驗證碼字元集**：純數字 6 碼？還是英數混合（不含易混淆字元）？—— 建議英數，避免撞碼
2. **綁定頁授權**：僅 `Tutor` / `SectionChief` / `BranchDirector` 三個角色顯示入口，還是所有登入者都能看到？
3. **Bot 好友連結**：用 QR code 圖片（要不要引入 QR 產生函式庫如 `QRCoder`）還是純文字連結？
4. **推播訊息格式**：純文字？還是 LINE Flex Message（卡片）？—— MVP 建議純文字，未來再升級
5. **是否要「測試發送」按鈕**：綁定頁上放一個「發送測試訊息到我的 LINE」讓老師驗證通道有效？—— 建議要
6. **多筆綁定**：同一老師換手機重綁時，是覆蓋舊 `LineUserId` 還是保留？—— 建議覆蓋（單一綁定）
7. **是否改走 Telegram / Discord**：既然 LINE 免費配額不夠，是否直接改用 Telegram Bot（無配額）？

---

## 八、後續 Phase 10+ 保留議題

- 學員端 LINE 綁定（讓學員收到 `FullyApproved` / `Rejected` 推播）
- 推播內容升級為 Flex Message（含「一鍵開啟」按鈕跳系統）
- 排程提醒：長時間未處理的簽核案件每日推播提醒導師（需背景服務 `IHostedService`）
- 推播統計儀表板（成功/失敗次數、月用量）
- **Telegram Bot 通道**：免費且無配額限制，可作為 LINE 的替代方案
- **Discord Webhook**：若組織已在用 Discord，可直接推頻道
