namespace LeaveSystem.Services.Export;

/// <summary>
/// 匯出欄位的值型別，讓輸出器（CSV / Excel）能決定格式化方式與 Excel 儲存格型別。
/// </summary>
/// <remarks>
/// - <see cref="Text"/>：純文字，直接 <c>ToString()</c>。
/// - <see cref="Date"/>：日期，CSV 用 <c>yyyy-MM-dd</c>；Excel 為原生日期型別（可排序 / 篩選）。
/// - <see cref="DateTime"/>：日期時間，CSV 用 <c>yyyy-MM-dd HH:mm</c>；Excel 為原生日期型別。
/// - <see cref="Number"/>：整數或一般數值，Excel 為數字型別。
/// - <see cref="Hours"/>：時數（decimal），CSV 用 <c>0.##</c>；Excel 為數字型別、儲存格格式 <c>#,##0.##</c>。
/// </remarks>
public enum ExportValueType
{
    Text = 0,
    Date = 1,
    DateTime = 2,
    Number = 3,
    Hours = 4,
}

/// <summary>
/// 匯出用的單一欄位定義。純輸出用 DTO，與 domain 無關；同一份定義可餵給 CSV 與 Excel 輸出器。
/// </summary>
/// <typeparam name="T">來源列型別。</typeparam>
public sealed class ExportColumn<T>
{
    public ExportColumn(string header, Func<T, object?> value, ExportValueType type = ExportValueType.Text)
    {
        Header = header ?? throw new ArgumentNullException(nameof(header));
        Value = value ?? throw new ArgumentNullException(nameof(value));
        Type = type;
    }

    public string Header { get; }

    public Func<T, object?> Value { get; }

    public ExportValueType Type { get; }
}

/// <summary>
/// 多 sheet Excel 匯出用的單一 sheet 描述（型別擦除，讓一次呼叫可放異質資料）。
/// 由 <see cref="ExportSheet.Create{T}"/> 建構，內部把 rows / columns 打包為預先展開的資料。
/// </summary>
public sealed class ExportSheet
{
    private ExportSheet(string name, IReadOnlyList<string> headers, IReadOnlyList<IReadOnlyList<(object? Value, ExportValueType Type)>> rows)
    {
        Name = name;
        Headers = headers;
        Rows = rows;
    }

    public string Name { get; }
    public IReadOnlyList<string> Headers { get; }
    public IReadOnlyList<IReadOnlyList<(object? Value, ExportValueType Type)>> Rows { get; }

    public static ExportSheet Create<T>(string name, IEnumerable<T> rows, IReadOnlyList<ExportColumn<T>> columns)
    {
        if (string.IsNullOrWhiteSpace(name)) throw new ArgumentException("sheet 名稱不可為空", nameof(name));
        ArgumentNullException.ThrowIfNull(rows);
        ArgumentNullException.ThrowIfNull(columns);

        var headers = columns.Select(c => c.Header).ToArray();
        var expanded = rows
            .Select(r => (IReadOnlyList<(object?, ExportValueType)>)columns.Select(c => (c.Value(r), c.Type)).ToArray())
            .ToArray();

        return new ExportSheet(name, headers, expanded);
    }
}
