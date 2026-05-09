namespace GmesImporter.Core.Models;

public sealed record ImportSummary(int Inserted, int Updated, int Skipped)
{
    public int Total => Inserted + Updated + Skipped;
}
