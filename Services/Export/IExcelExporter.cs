namespace LeaveSystem.Services.Export;

/// <summary>
/// Excel（.xlsx）匯出器：以 ClosedXML 產生檔案位元組。
/// 兩種呼叫模式：
///   - <see cref="Export{T}"/>：單一 sheet。
///   - <see cref="ExportWorkbook"/>：多 sheet（讓匯出頁一次帶請假明細 / 曠課明細 / 彙總）。
/// </summary>
public interface IExcelExporter
{
    byte[] Export<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns, string? sheetName = null);

    byte[] ExportWorkbook(IEnumerable<ExportSheet> sheets);
}
