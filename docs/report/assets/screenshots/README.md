# 截圖資產目錄

本資料夾存放專題報告投影片使用的截圖。

投影片端不需要逐頁改 HTML：`assets/slides.js` 會掃描每個 `<figure data-shot="xxx">`，
自動嘗試載入本資料夾的 `xxx.png`。

- **載入成功** → figure 加上 `.has-shot`，顯示圖片（`slides.css` 會藏掉虛線佔位）。
- **載入失敗**（還沒補圖）→ 維持原本的虛線佔位框，**版面不會壞**。

因此要補新圖，只要把檔案放進本資料夾、檔名對上 `data-shot` 即可。

## 產生方式（可重跑）

截圖由 `tools/ScreenshotCapture` 自動擷取：以 Playwright 驅動**系統既有的 Chrome**
（`Channel = "chrome"`，不下載額外瀏覽器），啟動網站於 `http://localhost:5199`，
用示範帳號逐頁登入擷取。

```powershell
# 1. 寫入示範資料（冪等；帳號前綴 demo.，密碼 Demo@123）
dotnet run --project tools/ScreenshotCapture -- --seed

# 2. 擷取全部截圖到本資料夾
dotnet run --project tools/ScreenshotCapture -- --capture

# 3. 截完後移除示範資料（既有真實資料完全不受影響）
dotnet run --project tools/ScreenshotCapture -- --rollback
```

其他模式：

| 指令 | 用途 |
|------|------|
| `--all` | `--seed` 後接 `--capture` |
| `--mocks` | 只重繪 `tools/ScreenshotCapture/mocks/` 下的示意圖，不啟動網站 |
| `--preview docs/report <輸出目錄>` | 把每張投影片單獨渲染成 PNG，用來檢查有沒有破版 |

**規格**：1600×900 視窗 × `DeviceScaleFactor = 2`（輸出 3200×1800），
`zh-TW` 語系、`Asia/Taipei` 時區，`FullPage = false` 以維持 16:9 比例。

## 截圖清單

| # | data-shot key | 狀態 | 擷取角色 | 頁面路徑 |
|---|--------------|------|---------|---------|
| 1 | `paper-form` | ✅ 已補 | — | `mocks/paper-form.html`（導入前情境示意圖） |
| 2 | `home-page` | ✅ 已補 | demo.stu01 | `/`（**需登入**，`HomeController.Index` 有 `[Authorize]`） |
| 3 | `login` | ✅ 已補 | 未登入 | `/Account/Login` |
| 4 | `first-login` | ✅ 已補 | 未登入 | `/Account/Login`（已填入預設 `Admin` 帳密、尚未送出） |
| 5 | `user-list` | ✅ 已補 | demo.admin | `/Admin/Users` |
| 6 | `cohort-edit` | ✅ 已補 | demo.admin | `/Admin/Cohorts/Edit/{示範班期Id}` |
| 7 | `leave-type-list` | ✅ 已補 | demo.admin | `/Admin/LeaveTypes` |
| 8 | `approval-rule` | ✅ 已補 | demo.admin | `/Admin/ApprovalRules` |
| 9 | `leave-create` | ✅ 已補 | demo.stu01 | `/LeaveRequests/Create`（表單已填好） |
| 10 | `leave-my` | ✅ 已補 | demo.stu01 | `/LeaveRequests` |
| 11 | `approvals-index` | ✅ 已補 | demo.tutor | `/Approvals` |
| 12 | `approval-details` | ✅ 已補 | demo.tutor | `/Approvals/Details/{id}` |
| 13 | `reports-index` | ✅ 已補 | demo.stu01 | `/Reports` |
| 14 | `reports-summary` | ✅ 已補 | demo.stu01 | `/Reports/Summary` |
| 15 | `reports-admin` | ✅ 已補 | demo.staff | `/Reports/Admin` |
| 16 | `absence-admin` | ✅ 已補 | demo.staff | `/Admin/AbsenceRecords` |
| 17 | `absence-my` | ✅ 已補 | demo.stu01 | `/AbsenceRecords` |
| 18 | `notification-badge` | ✅ 已補 | demo.stu01 | 導覽列元素截圖（`header`，含 🔔 未讀徽章） |
| 19 | `notification-list` | ✅ 已補 | demo.stu01 | `/Notifications` |
| 20 | `export-page` | ✅ 已補 | demo.staff | `/Admin/Export` |
| 21 | `export-excel` | ⬜ 未補 | — | 需開啟下載的 `.xlsx` 實際擷取 Excel 視窗，非瀏覽器畫面 |
| 22 | `student-flow` | ⬜ 未補 | — | 學員流程拼貼（送單 → 查進度 → 收通知） |
| 23 | `approver-flow` | ⬜ 未補 | — | 簽核人流程拼貼（待辦 → 詳情 → 核准/駁回） |
| 24 | `staff-flow` | ⬜ 未補 | — | 行政流程拼貼（建立 → 查詢 → 匯出） |
| 25 | `mail-pickup-eml` | ✅ 已補 | — | `mocks/mail-pickup-eml.html`（PickupDirectory 落地信件，見下方說明） |

未補的 4 張在投影片中維持虛線佔位，不影響版面。

### 關於 `mail-pickup-eml`

這張圖走 `--mocks` 產生，但**內容不是編造的**：`Capturer.CaptureMailPickupMockAsync` 會當場呼叫
網站專案裡真正的 `MailKitEmailSender`（`Mode = PickupDirectory`）寄出四封通知信到暫存目錄，
主旨／內文沿用 `NotificationDispatcher` 四個事件的實際文案，再把產生的檔名與 `.eml` 原文
讀回來注入 mock 頁渲染。截完圖暫存目錄即刪除，不會在專案裡留下 `.eml`。

因此**改了 `MailKitEmailSender` 或通知文案後，重跑 `--mocks` 就會得到最新的畫面**，
不需要手動修圖。

## 示範資料

`--seed` 只做**新增**，不修改也不刪除任何既有資料列（既有帳號的密碼雜湊尤其不動）：

- 新建 1 個班期 `115-1 示範班（報告截圖用）`（總時數 900、上限 10% ＝ 90 小時）
- 新建 9 個 `demo.*` 帳號（3 學員 / 導師 / 科長 / 分署長 / 行政 / 管理員），密碼一律 `Demo@123`
- 11 張涵蓋 Pending（停在第 1/2/3 關）、Approved、Rejected、Cancelled 的請假單與簽核步驟
- 5 筆曠課紀錄、6 則站內通知（含未讀，讓 🔔 徽章有數字）

`--rollback` 以 `demo.` 帳號前綴與示範班期名稱反查，一次移除上述全部資料。
