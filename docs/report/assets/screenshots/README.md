# 截圖資產目錄

本資料夾存放專題報告投影片使用的實際截圖。目前所有位置皆為虛線佔位 (`<figure class="screenshot-placeholder" data-shot="...">`)，補圖時將 PNG 檔命名為 `{data-shot}.png` 放入此資料夾即可（可在 CSS 中改為自動載入，或維持佔位以避免破版）。

## 命名規則

- 檔名：`{data-shot}.png`（小寫、連字號分隔）
- 建議尺寸：1600×900（16:9）或 1280×720
- 格式：PNG（若需含透明背景）或 JPG

## 截圖清單

| # | data-shot key | 對應章節 | 頁面路徑 | 建議畫面狀態 |
|---|--------------|---------|---------|-------------|
| 1 | `paper-form` | 01 動機 | — | 舊紙本假單 / LINE 群組訊息示意 |
| 2 | `home-page` | 02 簡介 | `/` | 未登入首頁 |
| 3 | `login` | 03.1 帳號 | `/Account/Login` | 登入頁面（可含錯誤提示） |
| 4 | `user-list` | 03.1 帳號 | `/Admin/Users` | 使用者列表（含多角色資料） |
| 5 | `cohort-edit` | 03.2 班期 | `/Admin/Cohorts/Edit/1` | 班期編輯頁（指派導師/科長/分署長） |
| 6 | `leave-type-list` | 03.2 假別 | `/Admin/LeaveTypes` | 假別清單 |
| 7 | `approval-rule` | 03.2 規則 | `/Admin/ApprovalRules` | 簽核門檻設定 |
| 8 | `leave-create` | 03.3 請假 | `/LeaveRequests/Create` | 學員送單表單（含時段下拉） |
| 9 | `leave-my` | 03.3 請假 | `/LeaveRequests` | 學員的我的請假單 |
| 10 | `approvals-index` | 03.4 簽核 | `/Approvals` | 簽核人待辦清單 |
| 11 | `approval-details` | 03.4 簽核 | `/Approvals/Details/1` | 單張簽核詳情（核准/駁回按鈕） |
| 12 | `reports-index` | 03.5 報表 | `/Reports` | 學員紀錄查詢（三條件篩選） |
| 13 | `reports-summary` | 03.5 報表 | `/Reports/Summary` | 學員累計時數 + 班期上限進度條 |
| 14 | `reports-admin` | 03.5 報表 | `/Reports/Admin` | 行政彙總報表 |
| 15 | `absence-admin` | 03.6 曠課 | `/Admin/AbsenceRecords` | 行政曠課列表 |
| 16 | `absence-my` | 03.6 曠課 | `/AbsenceRecords` | 學員自查曠課 |
| 17 | `notification-badge` | 03.7 通知 | 任一頁 | 導覽列 🔔 未讀徽章 |
| 18 | `notification-list` | 03.7 通知 | `/Notifications` | 通知中心 |
| 19 | `export-page` | 03.8 匯出 | `/Admin/Export` | 匯出設定頁 |
| 20 | `export-excel` | 03.8 匯出 | — | 匯出的 Excel 多 sheet 畫面 |
| 21 | `first-login` | 04 使用 | `/Account/Login` | 使用預設 `Admin/Admin@123` 登入 |
| 22 | `student-flow` | 04 使用 | 多頁 | 學員送單完整流程截圖拼貼 |
| 23 | `approver-flow` | 04 使用 | 多頁 | 簽核人處理流程截圖拼貼 |
| 24 | `staff-flow` | 04 使用 | 多頁 | 行政管理與匯出流程截圖拼貼 |

## 補圖流程

1. 於本機以預設 `Admin / Admin@123` 登入，補齊測試資料。
2. 依上表逐一擷取對應畫面，存成 `{key}.png` 放入本目錄。
3. 若要讓 HTML 自動顯示截圖取代佔位，於 `assets/slides.css` 加上：
	```css
	figure.screenshot-placeholder[data-shot="login"] .ph-box {
	  background: url("./screenshots/login.png") center/contain no-repeat;
	  border: 0;
	}
	figure.screenshot-placeholder[data-shot="login"] .ph-box::before { content: ""; }
	```
   （或以 JS 統一掃描 `[data-shot]` 動態注入 `<img>`。）
