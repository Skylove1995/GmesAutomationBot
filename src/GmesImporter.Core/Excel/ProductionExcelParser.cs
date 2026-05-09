using ClosedXML.Excel;
using GmesImporter.Core.Models;
using GmesImporter.Core.Parsing;

namespace GmesImporter.Core.Excel;

public static class ProductionExcelParser
{
    private static readonly string[] RequiredHeaders =
    {
        "Wip S/N",
        "Model",
        "Work Order",
        "Date"
    };

    public static IReadOnlyList<ProductionRecord> Parse(string filePath)
    {
        if (!File.Exists(filePath))
        {
            throw new FileNotFoundException("GMES Excel file was not found.", filePath);
        }

        using var workbook = new XLWorkbook(filePath);
        var worksheet = workbook.Worksheets.First();
        var headerRow = FindHeaderRow(worksheet);
        var headers = BuildHeaderMap(headerRow);
        EnsureRequiredHeaders(headers);

        var records = new List<ProductionRecord>();
        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? headerRow.RowNumber();

        for (var rowNumber = headerRow.RowNumber() + 1; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var pid = ReadString(row, headers["Wip S/N"]);
            if (string.IsNullOrWhiteSpace(pid))
            {
                continue;
            }

            records.Add(new ProductionRecord(
                ReadString(row, headers["Work Order"]),
                ReadString(row, headers["Model"]),
                pid,
                ReadDate(row, headers["Date"])));
        }

        return records;
    }

    private static IXLRow FindHeaderRow(IXLWorksheet worksheet)
    {
        var rowsToScan = worksheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var rowNumber = 1; rowNumber <= rowsToScan; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);
            var values = row.CellsUsed().Select(cell => cell.GetString().Trim()).ToHashSet(StringComparer.OrdinalIgnoreCase);
            if (RequiredHeaders.Any(values.Contains))
            {
                return row;
            }
        }

        throw new InvalidDataException("Could not find a GMES Inspection History header row in the Excel file.");
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        return headerRow.CellsUsed()
            .Where(cell => !string.IsNullOrWhiteSpace(cell.GetString()))
            .GroupBy(cell => cell.GetString().Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.First().Address.ColumnNumber, StringComparer.OrdinalIgnoreCase);
    }

    private static void EnsureRequiredHeaders(IReadOnlyDictionary<string, int> headers)
    {
        var missingHeaders = RequiredHeaders.Where(header => !headers.ContainsKey(header)).ToArray();
        if (missingHeaders.Length > 0)
        {
            throw new InvalidDataException($"GMES Excel file is missing required columns: {string.Join(", ", missingHeaders)}.");
        }
    }

    private static string ReadString(IXLRow row, int columnNumber)
    {
        return row.Cell(columnNumber).GetFormattedString().Trim();
    }

    private static DateTime? ReadDate(IXLRow row, int columnNumber)
    {
        var cell = row.Cell(columnNumber);
        if (cell.IsEmpty())
        {
            return null;
        }

        if (cell.TryGetValue<DateTime>(out var date))
        {
            return date;
        }

        if (cell.TryGetValue<double>(out var serialDate))
        {
            return DateTime.FromOADate(serialDate);
        }

        return GmesDateTimeNormalizer.Normalize(cell.GetFormattedString());
    }
}
