using GmesImporter.Core.Parsing;

namespace GmesImporter.Tests;

public sealed class DateTimeNormalizerTests
{
    [Theory]
    [InlineData("4/28/2026 11:06:30 AM", 2026, 4, 28, 11, 6, 30)]
    [InlineData("4/28/2026 23:06:30", 2026, 4, 28, 23, 6, 30)]
    [InlineData("2026-04-28 11:06:30", 2026, 4, 28, 11, 6, 30)]
    public void Normalize_parses_supported_text_formats(
        string value,
        int year,
        int month,
        int day,
        int hour,
        int minute,
        int second)
    {
        var result = GmesDateTimeNormalizer.Normalize(value);

        Assert.Equal(new DateTime(year, month, day, hour, minute, second), result);
    }

    [Fact]
    public void Normalize_converts_excel_serial_date()
    {
        var result = GmesDateTimeNormalizer.Normalize("46140.5");

        Assert.Equal(new DateTime(2026, 4, 28, 12, 0, 0), result);
    }

    [Theory]
    [InlineData("")]
    [InlineData(" ")]
    [InlineData(null)]
    public void Normalize_returns_null_for_empty_values(string? value)
    {
        var result = GmesDateTimeNormalizer.Normalize(value);

        Assert.Null(result);
    }

    [Fact]
    public void Normalize_throws_clear_error_for_invalid_values()
    {
        var exception = Assert.Throws<FormatException>(() => GmesDateTimeNormalizer.Normalize("not a date"));

        Assert.Contains("not a date", exception.Message);
    }
}
