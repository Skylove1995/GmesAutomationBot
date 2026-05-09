using ClosedXML.Excel;
using GmesImporter.Core.Excel;

namespace GmesImporter.Tests;

public sealed class ExcelProductionParserTests
{
    [Fact]
    public void Parse_reads_required_gmes_columns_by_header_name()
    {
        var filePath = CreateWorkbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Inspection History");
            sheet.Cell(1, 1).Value = "Wip S/N";
            sheet.Cell(1, 2).Value = "Model";
            sheet.Cell(1, 3).Value = "Work Order";
            sheet.Cell(1, 4).Value = "Date";

            sheet.Cell(2, 1).Value = "604HS2L7551";
            sheet.Cell(2, 2).Value = "EBR42255401";
            sheet.Cell(2, 3).Value = "6E1T5887-0002";
            sheet.Cell(2, 4).Value = new DateTime(2026, 4, 28, 11, 6, 30);
        });

        var records = ProductionExcelParser.Parse(filePath).ToList();

        Assert.Single(records);
        Assert.Equal("6E1T5887-0002", records[0].WorkOrder);
        Assert.Equal("EBR42255401", records[0].Ebr);
        Assert.Equal("604HS2L7551", records[0].Pid);
        Assert.Equal(new DateTime(2026, 4, 28, 11, 6, 30), records[0].Date);
    }

    [Fact]
    public void Parse_skips_rows_without_pid()
    {
        var filePath = CreateWorkbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Inspection History");
            sheet.Cell(1, 1).Value = "Wip S/N";
            sheet.Cell(1, 2).Value = "Model";
            sheet.Cell(1, 3).Value = "Work Order";
            sheet.Cell(1, 4).Value = "Date";
            sheet.Cell(2, 1).Value = "";
            sheet.Cell(2, 2).Value = "EBR42255401";
            sheet.Cell(2, 3).Value = "6E1T5887-0002";
        });

        var records = ProductionExcelParser.Parse(filePath).ToList();

        Assert.Empty(records);
    }

    [Fact]
    public void Parse_reports_missing_required_headers()
    {
        var filePath = CreateWorkbook(workbook =>
        {
            var sheet = workbook.AddWorksheet("Sheet1");
            sheet.Cell(1, 1).Value = "SomeOtherColumn";
        });

        var exception = Assert.Throws<InvalidDataException>(() => ProductionExcelParser.Parse(filePath).ToList());

        Assert.Contains("Wip S/N", exception.Message);
    }

    private static string CreateWorkbook(Action<XLWorkbook> configure)
    {
        var filePath = Path.Combine(Path.GetTempPath(), $"{Guid.NewGuid():N}.xlsx");
        using var workbook = new XLWorkbook();
        configure(workbook);
        workbook.SaveAs(filePath);
        return filePath;
    }
}
