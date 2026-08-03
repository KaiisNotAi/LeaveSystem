using System.Globalization;
using System.Text;

namespace LeaveSystem.Services.Export;

/// <summary>
/// <see cref="ICsvExporter"/> 的預設實作。
/// - 編碼：UTF-8 with BOM（讓 Excel 開啟中文不亂碼）。
/// - 換行：CRLF（Excel 相容）。
/// - 跳脫：RFC 4180 — 欄位含逗號 / 雙引號 / CR / LF 時整欄用雙引號包起，內部雙引號重覆一次。
/// - 空值：一律輸出空字串。
/// - 格式：
///     * <see cref="ExportValueType.Date"/> → <c>yyyy-MM-dd</c>
///     * <see cref="ExportValueType.DateTime"/> → <c>yyyy-MM-dd HH:mm</c>
///     * <see cref="ExportValueType.Hours"/> / <see cref="ExportValueType.Number"/> → <c>0.##</c>（decimal / double）或直接 <c>ToString(CultureInfo.InvariantCulture)</c>
/// </summary>
public sealed class CsvExporter : ICsvExporter
{
    private static readonly byte[] Utf8Bom = { 0xEF, 0xBB, 0xBF };
    private static readonly Encoding Utf8NoBom = new UTF8Encoding(false);

    public byte[] Export<T>(IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns)
    {
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        using var ms = new MemoryStream();
        ms.Write(Utf8Bom, 0, Utf8Bom.Length);

        using (var writer = new StreamWriter(ms, Utf8NoBom, leaveOpen: true) { NewLine = "\r\n" })
        {
            // Header
            writer.WriteLine(string.Join(',', columns.Select(c => EscapeField(c.Header))));

            // Rows
            foreach (var row in rows)
            {
                var cells = columns.Select(c => EscapeField(FormatValue(c.Value(row), c.Type)));
                writer.WriteLine(string.Join(',', cells));
            }
        }

        return ms.ToArray();
    }

    private static string FormatValue(object? value, ExportValueType type)
    {
        if (value is null) return string.Empty;

        return type switch
        {
            ExportValueType.Date when value is DateTime dt => dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ExportValueType.Date when value is DateOnly d => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            ExportValueType.DateTime when value is DateTime dt => dt.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture),
            ExportValueType.Hours when value is decimal dec => dec.ToString("0.##", CultureInfo.InvariantCulture),
            ExportValueType.Hours when value is double dbl => dbl.ToString("0.##", CultureInfo.InvariantCulture),
            ExportValueType.Number when value is IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => value.ToString() ?? string.Empty,
        };
    }

    private static string EscapeField(string field)
    {
        if (string.IsNullOrEmpty(field)) return string.Empty;

        var needsQuote = field.IndexOfAny(new[] { ',', '"', '\r', '\n' }) >= 0;
        if (!needsQuote) return field;

        return "\"" + field.Replace("\"", "\"\"") + "\"";
    }
}
