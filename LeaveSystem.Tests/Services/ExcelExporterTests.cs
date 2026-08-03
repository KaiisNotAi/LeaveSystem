using ClosedXML.Excel;
using LeaveSystem.Services.Export;

namespace LeaveSystem.Tests.Services;

public class ExcelExporterTests
{
    private readonly IExcelExporter _sut = new ClosedXmlExcelExporter();

    private sealed record Row(string Name, DateTime Date, decimal Hours, int Count);

    private static readonly IReadOnlyList<ExportColumn<Row>> Columns = new[]
    {
        new ExportColumn<Row>("姓名", r => r.Name, ExportValueType.Text),
        new ExportColumn<Row>("日期", r => r.Date, ExportValueType.Date),
        new ExportColumn<Row>("時數", r => r.Hours, ExportValueType.Hours),
        new ExportColumn<Row>("次數", r => r.Count, ExportValueType.Number),
    };

    private static readonly Row[] SampleRows =
    {
        new("王小明", new DateTime(2026, 8, 15), 3.5m, 1),
        new("李小花", new DateTime(2026, 8, 16), 4m, 2),
    };

    private static XLWorkbook Open(byte[] bytes) => new(new MemoryStream(bytes));

    [Fact]
    public void Export_ShouldProduceReadableXlsxWithDefaultSheetName()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        Assert.Equal("報表", ws.Name);
    }

    [Fact]
    public void Export_ShouldUseGivenSheetName()
    {
        var bytes = _sut.Export(SampleRows, Columns, "請假明細");

        using var wb = Open(bytes);
        Assert.NotNull(wb.Worksheet("請假明細"));
    }

    [Fact]
    public void Export_HeaderRow_ShouldBeBold()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        Assert.Equal("姓名", ws.Cell(1, 1).GetString());
        Assert.True(ws.Cell(1, 1).Style.Font.Bold);
        Assert.True(ws.Cell(1, 4).Style.Font.Bold);
    }

    [Fact]
    public void Export_ShouldWriteAllDataRows()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        Assert.Equal("王小明", ws.Cell(2, 1).GetString());
        Assert.Equal("李小花", ws.Cell(3, 1).GetString());
        Assert.True(ws.Cell(4, 1).IsEmpty());
    }

    [Fact]
    public void Export_HoursCell_ShouldBeNumeric()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        var hoursCell = ws.Cell(2, 3);
        Assert.Equal(XLDataType.Number, hoursCell.DataType);
        Assert.Equal(3.5, hoursCell.GetDouble(), 3);
    }

    [Fact]
    public void Export_DateCell_ShouldBeDateType()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        var dateCell = ws.Cell(2, 2);
        Assert.Equal(XLDataType.DateTime, dateCell.DataType);
        Assert.Equal(new DateTime(2026, 8, 15), dateCell.GetDateTime());
    }

    [Fact]
    public void Export_ShouldFreezeFirstRow()
    {
        var bytes = _sut.Export(SampleRows, Columns);

        using var wb = Open(bytes);
        var ws = wb.Worksheets.First();
        Assert.Equal(1, ws.SheetView.SplitRow);
    }

    [Fact]
    public void ExportWorkbook_ShouldCreateMultipleSheetsWithIndependentData()
    {
        var leaveSheet = ExportSheet.Create("請假明細", SampleRows, Columns);
        var absenceSheet = ExportSheet.Create("曠課明細",
            new[] { new Row("翹課生", new DateTime(2026, 8, 20), 2m, 1) }, Columns);
        var summarySheet = ExportSheet.Create("彙總",
            new[] { new Row("王小明", new DateTime(2026, 8, 1), 3.5m, 1) }, Columns);

        var bytes = _sut.ExportWorkbook(new[] { leaveSheet, absenceSheet, summarySheet });

        using var wb = Open(bytes);
        Assert.Equal(3, wb.Worksheets.Count);
        Assert.NotNull(wb.Worksheet("請假明細"));
        Assert.NotNull(wb.Worksheet("曠課明細"));
        Assert.NotNull(wb.Worksheet("彙總"));

        Assert.Equal("翹課生", wb.Worksheet("曠課明細").Cell(2, 1).GetString());
        Assert.Equal("王小明", wb.Worksheet("彙總").Cell(2, 1).GetString());
    }
}
