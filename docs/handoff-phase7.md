# Phase 7 交接文件 — 站內通知 + Email

> 用途：讓下一個對話（新 session）能直接接手 Phase 7 開發。請先讀本檔 → 讀 `docs/05-開發階段與里程碑.md` 對應段落 → 開始 TDD 實作。

## 專案原則（沿用）

- 語言：zh-TW
- 開發流程：**TDD 優先**（先寫測試 → 再實作）— 出自 `.github/copilot-instructions.md`
- 每個 Phase：開 `feature/phaseN-xxx` 分支 → 完成後 `git merge --no-ff master` + 打 tag（策略見 `docs/05` L7–11）
- 目標框架：.NET 10，Razor Pages/MVC 混合（本專案主要為 MVC Controllers + Views）

## 目前 Git 狀態（Phase 6 收尾完成時的快照）

- `master` @ `16dddf7`，tag `v0.7-phase6` 已推送到 origin
- **當前分支：`feature/phase7-notifications`（已建立、尚無 commit）**
- 41/41 測試通過、build 0 錯誤（僅 2 個既有 warning：`Views/Home/Index.cshtml` CS8602、`AccountController` ASP0026）

## Phase 7 目標（摘自 `docs/05` L182–188）

> 簽核時自動推送通知（站內 + Email）。

**交付物**：
1. `NotificationService`（站內通知：建立、查詢、標記已讀）
2. `IEmailSender` + `MailKitEmailSender`（MailKit 已在 Phase 0 加為 NuGet）
3. 通知中心頁面（列表、未讀徽章）
4. 與 `ApprovalService` 及 `LeaveRequestsController` 整合觸發點

## 已就緒的基礎建設（無需重建）

| 項目 | 位置 | 說明 |
|---|---|---|
| `Notification` entity | `Models/Entities/Notification.cs` | Phase 0 已完成，含 `UserId / Title / Message / Url / IsRead / CreatedAt / ReadAt`，導覽 `User` |
| `Notifications` DbSet | `Data/AppDbContext.cs` | 已註冊，資料表已在 InitialCreate migration 建立 |
| MailKit 套件 | `LeaveSystem.csproj` | Phase 0 已加，不用再裝 |
| Layout 導覽列 | `Views/Shared/_Layout.cshtml` | 需新增「通知中心」入口 + 未讀徽章 |

## 建議實作步驟（給下個對話的骨架）

先跟使用者確認以下設計點再開始 code：

### 需澄清問題
1. **Email 發送策略**：同步（簽核當下寄）／非同步（背景 Job）？初期建議同步 + try/catch 不影響簽核。
2. **Email 開發環境**：走 SMTP（如 Papercut / MailHog）還是純寫檔？建議 `appsettings.Development.json` 設 `Email:Mode = "PickupDirectory"` 落地到 `./mail-pickup/`。
3. **通知觸發點**（建議覆蓋這些事件）：
   - 學員送出申請 → 通知**下一位簽核人**
   - 上一級簽核通過 → 通知**下一位簽核人**
   - 全流程通過 → 通知**學員**
   - 任一級駁回 → 通知**學員**
4. **通知中心 UI**：獨立頁 `/Notifications` 還是下拉選單？建議獨立頁 + Layout 徽章顯示未讀數。
5. **是否納入 Email 內容 template**：初期用純字串內插即可，後期再抽 Razor template。

### TDD 實作骨架（12 步）

1. 讀 `Services/IApprovalService.cs` 與 `ApprovalService.cs`，找出所有狀態變動點（Approve、Reject、送出）
2. 讀 `Controllers/LeaveRequestsController.cs` 找送出點
3. `LeaveSystem.Tests/Services/NotificationServiceTests.cs` — 撰寫規格測試（建立通知、查未讀、標記已讀、批次標記已讀、不同 UserId 隔離）
4. 實作 `INotificationService` + `NotificationService`
5. `LeaveSystem.Tests/Services/EmailSenderTests.cs` — 用 fake `IEmailSender` 驗證呼叫；MailKit 實作用整合測試（可選）
6. 實作 `IEmailSender` + `MailKitEmailSender` + `NullEmailSender`（開發用，寫 log 或 pickup 目錄）
7. `Program.cs` 註冊兩個服務、綁定 `EmailOptions`
8. 建立 `NotificationDispatcher`（或直接在 `ApprovalService` 內注入 `INotificationService` + `IEmailSender`）— 觸發 4 個事件
9. `ApprovalServiceTests` 補測試：呼叫 Approve/Reject 應觸發對應通知（用 Moq 或 fake）
10. 建 `NotificationsController` + Views：`Index`（列表 + 標記已讀）、`MarkAllRead` action
11. `_Layout.cshtml`：登入使用者顯示 🔔 + 未讀數徽章（考慮用 ViewComponent 避免每頁重寫 query）
12. 手動驗收 → 更新 `docs/05` L182 段落狀態 → 合併打 tag `v0.8-phase7`

### 建議檔案結構

```
Models/ViewModels/NotificationViewModels.cs
Services/INotificationService.cs
Services/NotificationService.cs
Services/Email/IEmailSender.cs
Services/Email/MailKitEmailSender.cs
Services/Email/NullEmailSender.cs      // Dev 用
Services/Email/EmailOptions.cs
ViewComponents/NotificationBadgeViewComponent.cs
Controllers/NotificationsController.cs
Views/Notifications/Index.cshtml
Views/Shared/Components/NotificationBadge/Default.cshtml
LeaveSystem.Tests/Services/NotificationServiceTests.cs
LeaveSystem.Tests/Services/ApprovalNotificationIntegrationTests.cs
```

### appsettings 建議片段

```json
"Email": {
  "Mode": "PickupDirectory",           // PickupDirectory | Smtp
  "PickupDirectory": "./mail-pickup",
  "From": "noreply@leavesystem.local",
  "Smtp": {
	"Host": "localhost",
	"Port": 25,
	"UseSsl": false,
	"Username": null,
	"Password": null
  }
}
```

## 驗收 checklist（Phase 7 結束前需全綠）

- [ ] 學員送單後，下一位簽核人收到站內通知 + Email
- [ ] 逐級簽核通過皆推播至下一位
- [ ] 駁回 → 通知學員含駁回原因
- [ ] 全流程通過 → 通知學員
- [ ] `/Notifications` 顯示登入者通知，可標記已讀、批次已讀
- [ ] Layout 徽章顯示未讀數，讀完歸零
- [ ] Email 內容含請假單連結（可點回系統）
- [ ] 開發模式使用 PickupDirectory 不真的寄信
- [ ] 所有既有測試仍綠、新增測試皆綠
- [ ] `docs/05` L182 段落狀態更新為完成

## 交接 checklist（給下個對話開場用）

下一個對話開始時，請執行：

1. `git branch --show-current` → 應為 `feature/phase7-notifications`
2. 讀 `docs/handoff-phase7.md`（本檔）
3. 讀 `docs/05-開發階段與里程碑.md` Phase 7 段落
4. 讀 `docs/07-專案結構說明.md` 了解專案佈局
5. 用 `ask_question` 向使用者確認上方「需澄清問題」5 項
6. 建立 `plan` 進入 TDD 實作
