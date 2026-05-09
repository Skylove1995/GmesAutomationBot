using System.Globalization;

namespace GmesImporter.Core.Parsing;

public static class GmesDateTimeNormalizer
{
    private static readonly string[] SupportedFormats =
    {
        "M/d/yyyy h:mm:ss tt",
        "M/d/yyyy h:mm tt",
        "M/d/yyyy H:mm:ss",
        "M/d/yyyy H:mm",
        "MM/dd/yyyy h:mm:ss tt",
        "MM/dd/yyyy HH:mm:ss",
        "yyyy-MM-dd HH:mm:ss",
        "yyyy-MM-dd H:mm:ss",
        "yyyy/M/d H:mm:ss",
        "yyyy/M/d h:mm:ss tt"
    };

    public static DateTime? Normalize(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (double.TryParse(trimmed, NumberStyles.Float, CultureInfo.InvariantCulture, out var serialDate)
            && serialDate > 0)
        {
            return DateTime.FromOADate(serialDate);
        }

        if (DateTime.TryParseExact(
                trimmed,
                SupportedFormats,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var exactDate))
        {
            return exactDate;
        }

        if (DateTime.TryParse(
                trimmed,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AllowWhiteSpaces,
                out var parsedDate))
        {
            return parsedDate;
        }

        throw new FormatException($"GMES datetime value '{trimmed}' is not in a supported format.");
    }
}
