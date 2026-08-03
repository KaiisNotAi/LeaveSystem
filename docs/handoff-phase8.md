# Phase 8 交接文件 — 報表匯出（CSV / Excel）

> 用途：讓下一個對話（新 session）能直接接手 Phase 8 開發。請先讀本檔 → 讀 `docs/05-開發階段與里程碑.md` 對應段落 → 開始 TDD 實作。
>
> ⚠️ 原 Phase 8「LINE Notify」已延後至 **Phase 9**，本階段專注做**報表匯出**。

## 專案原則（沿用）

- 語言：zh-TW
- 開發流程：**TDD 優先**（先寫測試 → 再實作）— 出自 `.github/copilot-instructions.md`
- 每個 Phase：開 `feature/phaseN-xxx` 分支 → 完成後 `git merge --no-ff master` + 打 tag（策略見 `docs/05`）
- 目標框架：.NET 10，MVC Controllers + Views

## 目前 Git 狀態（Phase 7 收尾完成時的快照）

- `master` @ `0787965`，tag `v0.8-phase7` 已推送到 origin
- **待建立分支：`feature/phase8-report-export`**
- 59/59 測試通過、build 0 錯誤（僅 2 個既有 warning：`Views/Home/Index.cshtml` CS8602、`AccountController` ASP0026）

## Phase 8 目標

> 讓行政與學員可將報表資料匯出為 CSV / Excel 檔，方便離線分析、上報主管、彙整。

## 已就緒的基礎建設（無需重建）

Phase 5 已建置完整報表 Service 層與頁面，Phase 8 只需要在其上加「匯出」動作即可。

| 項目 | 位置 | 說明 |
|---|---|---|
| `ILeaveReportService` / `LeaveReportService` | `Services/LeaveReportService.cs` | 已有 `GetMyRequestsAsync` / `GetMyLeaveTypeSummaryAsync` / `GetAdminReportAsync` |
| `ReportsController` | `Controllers/ReportsController.cs` | `Index`（我的紀錄）/ `Summary`（累計）/ `Admin`（彙總） |
| `IAbsenceRecordService` | `Services/AbsenceRecordService.cs` | Phase 6 曠課紀錄，也是可匯出候選 |
| `ReportTestScenario` | `LeaveSystem.Tests/Helpers/ReportTestScenario.cs` | 多筆多狀態測試資料工廠，可直接複用 |

## 建議實作步驟（給下個對話的骨架）

先跟使用者確認以下設計點再開始 code：

### 需澄清問題

1. **匯出格式**：CSV 為主／Excel 為主／兩者都做？初期建議「兩者都做，但 CSV 優先」（CSV 零套件、Excel 用 ClosedXML）。
2. **Excel 套件選擇**：
   - `ClosedXML`（MIT，最易用，內部走 OpenXml SDK）— **建議**
   - `EPPlus`（v5+ 商用需授權，個人 / 學術可免費）— 有授權疑慮
   - `DocumentFormat.OpenXml`（微軟原生，功能最全但 API 較底層）
3. **要在哪些頁面加匯出按鈕？**（建議全數）：
   - `/Reports`（學員自己的請假紀錄）
   - `/Reports/Summary`（學員各假別累計）
   - `/Reports/Admin`（行政彙總，最常用）
   - `/AbsenceRecords`（學員曠課）
   - `/Admin/AbsenceRecords`（行政曠課）
   - `/Approvals`（簽核工作台歷史紀錄）— 可選
4. **CSV 編碼**：UTF-8 with BOM（讓 Excel 開啟中文不亂碼）— 建議是。
5. **檔案匯出方式**：全部撈進記憶體再回傳（適合中小型資料）／streaming（適合大量資料）— 初期建議記憶體法即可，若日後單月資料超過數萬筆再改 streaming。
6. **檔名格式**：例如 `leave-requests-2026-08-15.csv` / `admin-report-2026-08.xlsx`？

### TDD 實作骨架（12 步）

1. 讀 `Services/LeaveReportService.cs`、`Controllers/ReportsController.cs`、`Services/AbsenceRecordService.cs`，確認要匯出的資料形狀。
2. 決定 Excel 套件並加入 NuGet（建議：`ClosedXML`）。
3. 建 `Services/Export/ICsvExporter.cs` + `CsvExporter.cs`（純函式：`IEnumerable<T> → byte[]`，UTF-8 BOM）。
4. 建 `LeaveSystem.Tests/Services/CsvExporterTests.cs` — 規格：欄位順序、含 BOM、逗號 / 引號 / 換行跳脫、日期格式一致。
5. 實作 `CsvExporter` 讓測試綠。
6. 建 `Services/Export/IExcelExporter.cs` + `ClosedXmlExcelExporter.cs`。
7. 建 `ExcelExporterTests.cs` — 用 ClosedXML 回讀 `.xlsx`，驗證 sheet 名稱、標題列、資料筆數、時數格式。
8. 實作 `ClosedXmlExcelExporter` 讓測試綠。
9. 建 `Services/Export/ReportExportModels.cs`（可選）— 集中匯出用的 DTO / 欄位對應。
10. `ReportsController` 加 `ExportCsv` / `ExportXlsx` action（每個報表頁一組），回傳 `FileContentResult`。
11. 各頁面 View 加匯出按鈕（表單 POST 或 GET 帶目前篩選條件）。
12. `AbsenceRecords`、`Admin/AbsenceRecords` 也比照加入匯出按鈕（Phase 6 頁面）。
13. `Program.cs` 註冊兩個匯出服務。
14. 全測試通過後手動驗收 → 更新 `docs/05` Phase 8 段落狀態 → 合併打 tag `v0.9-phase8`。

### 建議檔案結構

```
Services/Export/
	ICsvExporter.cs
	CsvExporter.cs
	IExcelExporter.cs
	ClosedXmlExcelExporter.cs
	ReportExportModels.cs            // 選用：集中匯出 DTO
LeaveSystem.Tests/Services/
	CsvExporterTests.cs
	ExcelExporterTests.cs
```

### Controller 範例（僅示意）

```csharp
// GET /Reports/ExportMyRequestsCsv?...同 Index 的 query...
public async Task<IActionResult> ExportMyRequestsCsv(MyRequestsQuery query)
{
	var items = await _reportService.GetMyRequestsAsync(GetCurrentUserId()!.Value, query);
	var bytes = _csv.Export(items, columns: new[]
	{
		("申請日", (LeaveRequest r) => r.CreatedAt.ToString("yyyy-MM-dd")),
		("假別",   (LeaveRequest r) => r.LeaveTypeName),
		// ...
	});
	return File(bytes, "text/csv", $"my-requests-{DateTime.Today:yyyyMMdd}.csv");
}
```

## 驗收 checklist（Phase 8 結束前需全綠）

- [ ] `/Reports` 學員紀錄可匯出 CSV 與 Excel，欄位與畫面一致
- [ ] `/Reports/Admin` 行政彙總可匯出 CSV 與 Excel，遵守目前篩選條件
- [ ] `/AbsenceRecords` 與 `/Admin/AbsenceRecords` 可匯出
- [ ] CSV 用 Excel 打開中文不亂碼（UTF-8 BOM）
- [ ] Excel 檔的時數欄為數字格式，可加總；日期欄為日期格式
- [ ] 逗號 / 引號 / 換行的資料在 CSV 內正確跳脫
- [ ] 未登入 / 非本人 / 非行政的匯出 URL 會被授權中介擋下
- [ ] 所有既有測試仍綠、新增測試皆綠
- [ ] `docs/05` Phase 8 段落狀態更新為完成

## 交接 checklist（給下個對話開場用）

下一個對話開始時，請執行：

1. `git branch --show-current` → 應為 `master`；先切 `git checkout -b feature/phase8-report-export`
2. 讀 `docs/handoff-phase8.md`（本檔）
3. 讀 `docs/05-開發階段與里程碑.md` Phase 8 段落
4. 讀 `docs/07-專案結構說明.md` 了解專案佈局
5. 用 `ask_question` 向使用者確認上方「需澄清問題」6 項
6. 建立 `plan` 進入 TDD 實作

## Phase 9 提示（下一個 phase 才做）

- **LINE Notify** 或 **LINE Messaging API**（LINE Notify 官方 2025-03-31 已停用，建議改走 LINE Messaging API + LIFF 綁定，或用 Webhook 走 Telegram / Discord）
- 具體選型由 Phase 9 開始時再議
