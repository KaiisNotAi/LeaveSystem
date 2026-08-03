using System.Text;
using LeaveSystem.Services.Export;

namespace LeaveSystem.Tests.Services;

public class CsvExporterTests
{
    private readonly ICsvExporter _sut = new CsvExporter();

    private sealed record Row(string? Name, DateTime? Date, decimal? Hours, int? Count, string? Note);

    private static readonly IReadOnlyList<ExportColumn<Row>> Columns = new[]
    {
        new ExportColumn<Row>("姓名", r => r.Name, ExportValueType.Text),
        new ExportColumn<Row>("日期", r => r.Date, ExportValueType.Date),
        new ExportColumn<Row>("時數", r => r.Hours, ExportValueType.Hours),
        new ExportColumn<Row>("次數", r => r.Count, ExportValueType.Number),
        new ExportColumn<Row>("備註", r => r.Note, ExportValueType.Text),
    };

    private static string DecodeUtf8(byte[] bytes) =>
        Encoding.UTF8.GetString(bytes, 3, bytes.Length - 3); // 去掉 BOM 後解碼

    [Fact]
    public void Export_ShouldStartWithUtf8Bom()
    {
        var bytes = _sut.Export(new[] { new Row("王小明", new DateTime(2026, 8, 15), 3.5m, 1, null) }, Columns);

        Assert.True(bytes.Length >= 3);
        Assert.Equal(0xEF, bytes[0]);
        Assert.Equal(0xBB, bytes[1]);
        Assert.Equal(0xBF, bytes[2]);
    }

    [Fact]
    public void Export_ShouldPreserveHeaderOrderAndFormatValues()
    {
        var bytes = _sut.Export(
            new[] { new Row("王小明", new DateTime(2026, 8, 15), 3.5m, 1, "OK") },
            Columns);

        var text = DecodeUtf8(bytes);
        var lines = text.Split("\r\n", StringSplitOptions.RemoveEmptyEntries);

        Assert.Equal("姓名,日期,時數,次數,備註", lines[0]);
        Assert.Equal("王小明,2026-08-15,3.5,1,OK", lines[1]);
    }

    [Fact]
    public void Export_ShouldFormatHoursWithMinimalDecimals()
    {
        var bytes = _sut.Export(
            new[]
            {
                new Row("整數", null, 4m, null, null),
                new Row("一位", null, 3.5m, null, null),
                new Row("兩位", null, 3.25m, null, null),
            },
            Columns);

        var lines = DecodeUtf8(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("整數,,4,,", lines[1]);
        Assert.Equal("一位,,3.5,,", lines[2]);
        Assert.Equal("兩位,,3.25,,", lines[3]);
    }

    [Fact]
    public void Export_ShouldOutputEmptyStringForNullValues()
    {
        var bytes = _sut.Export(
            new[] { new Row(null, null, null, null, null) },
            Columns);

        var lines = DecodeUtf8(bytes).Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal(",,,,", lines[1]);
    }

    [Fact]
    public void Export_ShouldEscapeCommaQuoteAndNewline()
    {
        var bytes = _sut.Export(
            new[]
            {
                new Row("含,逗號", null, null, null, "含\"引號"),
                new Row("含\r\n換行", null, null, null, "普通"),
            },
            Columns);

        var text = DecodeUtf8(bytes);

        // 含逗號 → 整欄用雙引號包起
        Assert.Contains("\"含,逗號\"", text);
        // 含雙引號 → 內部雙引號重覆一次，並整欄包起
        Assert.Contains("\"含\"\"引號\"", text);
        // 含換行 → 整欄用雙引號包起（值本身仍保留原始換行）
        Assert.Contains("\"含\r\n換行\"", text);
    }

    [Fact]
    public void Export_WithNoRows_ShouldReturnBomAndHeaderOnly()
    {
        var bytes = _sut.Export(Array.Empty<Row>(), Columns);
        var text = DecodeUtf8(bytes);

        Assert.Equal("姓名,日期,時數,次數,備註\r\n", text);
    }
}
