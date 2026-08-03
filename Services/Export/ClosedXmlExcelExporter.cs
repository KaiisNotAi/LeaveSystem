using ClosedXML.Excel;

namespace LeaveSystem.Services.Export;

/// <summary>
/// <see cref="IExcelExporter"/> 的 ClosedXML 實作。
///
/// 產出樣式：
///   ‧ 標題列（第 1 列）：粗體、凍結。
///   ‧ Hours 儲存格：數字型別 + 格式 <c>#,##0.##</c>（可加總 / 篩選）。
///   ‧ Number 儲存格：數字型別（未指定格式）。
///   ‧ Date 儲存格：日期型別 + 格式 <c>yyyy-mm-dd</c>。
///   ‧ DateTime 儲存格：日期型別 + 格式 <c>yyyy-mm-dd hh:mm</c>。
///   ‧ 匯出結束會 <c>AdjustToContents()</c>，讓欄寬自動貼合內容。
/// </summary>
public sealed class ClosedXmlExcelExporter : IExcelExporter
{
    private const string DefaultSheetName = "報表";
    private const string HoursFormat = "#,##0.##";
    private const string DateFormat = "yyyy-mm-dd";
    private const string DateTimeFormat = "yyyy-mm-dd hh:mm";

    public byte[] Export<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns, string? sheetName = null)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        using var wb = new XLWorkbook();
        WriteSheet(wb, sheetName ?? DefaultSheetName, columns.Select(c => c.Header).ToArray(),
            rows.Select(r => columns.Select(c => ((object?)c.Value(r), c.Type)).ToArray()));

        return SaveToBytes(wb);
    }

    public byte[] ExportWorkbook(IEnumerable<ExportSheet> sheets)
    {
        ArgumentNullException.ThrowIfNull(sheets);

        using var wb = new XLWorkbook();
        foreach (var sheet in sheets)
        {
            WriteSheet(wb, sheet.Name, sheet.Headers,
                sheet.Rows.Select(r => r.Select(cell => ((object?)cell.Value, cell.Type)).ToArray()));
        }

        // 若外部一個 sheet 都沒帶，至少放一張空 sheet 避免 xlsx 損毀。
        if (wb.Worksheets.Count == 0)
        {
            wb.Worksheets.Add(DefaultSheetName);
        }

        return SaveToBytes(wb);
    }

    private static void WriteSheet(
        XLWorkbook wb,
        string name,
        IReadOnlyList<string> headers,
        IEnumerable<(object? Value, ExportValueType Type)[]> rows)
    {
        var ws = wb.Worksheets.Add(SanitizeSheetName(name));

        // Header
        for (var i = 0; i < headers.Count; i++)
        {
            var cell = ws.Cell(1, i + 1);
            cell.Value = headers[i];
            cell.Style.Font.Bold = true;
        }

        // Rows
        var rowIndex = 2;
        foreach (var row in rows)
        {
            for (var c = 0; c < row.Length; c++)
            {
                WriteCell(ws.Cell(rowIndex, c + 1), row[c].Value, row[c].Type);
            }
            rowIndex++;
        }

        // 首列凍結 + 自動欄寬（含中文加寬補償）
        ws.SheetView.FreezeRows(1);
        ws.Columns().AdjustToContents();

        // ClosedXML 以 Calibri 半形字寬估算欄寬，中文全形字實際佔 2 個字寬，
        // 因此 AdjustToContents 對中文明顯偏窄。這裡做兩件事：
        //   1) 每欄寬度加 2 個字寬當作 padding（避免最後一個字被截）。
        //   2) 上限 60 避免超長備註把版面撐爆；下限 8 讓短標題也不會太擠。
        foreach (var col in ws.ColumnsUsed())
        {
            var w = col.Width + 2;
            if (w < 8) w = 8;
            if (w > 60) w = 60;
            col.Width = w;
        }
    }

    private static void WriteCell(IXLCell cell, object? value, ExportValueType type)
    {
        if (value is null)
        {
            // 保留空白；不設 Value
            return;
        }

        switch (type)
        {
            case ExportValueType.Date:
                if (value is DateTime d) { cell.Value = d.Date; cell.Style.DateFormat.Format = DateFormat; }
                else if (value is DateOnly dOnly) { cell.Value = dOnly.ToDateTime(TimeOnly.MinValue); cell.Style.DateFormat.Format = DateFormat; }
                else { cell.Value = value.ToString(); }
                break;

            case ExportValueType.DateTime:
                if (value is DateTime dt) { cell.Value = dt; cell.Style.DateFormat.Format = DateTimeFormat; }
                else { cell.Value = value.ToString(); }
                break;

            case ExportValueType.Hours:
                cell.Value = Convert.ToDouble(value);
                cell.Style.NumberFormat.Format = HoursFormat;
                break;

            case ExportValueType.Number:
                cell.Value = Convert.ToDouble(value);
                break;

            case ExportValueType.Text:
            default:
                cell.Value = value.ToString();
                break;
        }
    }

    /// <summary>
    /// Excel sheet 名稱不可超過 31 字元、不可含 <c>: \ / ? * [ ]</c>。
    /// </summary>
    private static string SanitizeSheetName(string name)
    {
        var cleaned = new string(name.Select(ch => ch switch
        {
            ':' or '\\' or '/' or '?' or '*' or '[' or ']' => '_',
            _ => ch,
        }).ToArray());

        return cleaned.Length <= 31 ? cleaned : cleaned[..31];
    }

    private static byte[] SaveToBytes(XLWorkbook wb)
    {
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        return ms.ToArray();
    }
}
