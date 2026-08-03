namespace LeaveSystem.Services.Export;

/// <summary>
/// CSV 匯出器：將任意列序列 + 欄位定義輸出為 UTF-8（含 BOM）位元組。
/// 符合 RFC 4180 跳脫規則（逗號 / 雙引號 / CR / LF 觸發整欄雙引號包起，內部雙引號重覆一次）。
/// </summary>
public interface ICsvExporter
{
    byte[] Export<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns);
}
