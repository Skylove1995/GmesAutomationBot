using GmesImporter.Core.Models;

namespace GmesImporter.Core.Importing;

public sealed class ProductionImportService
{
    private readonly IProductionRepository _repository;

    public ProductionImportService(IProductionRepository repository)
    {
        _repository = repository;
    }

    public async Task<ImportSummary> ImportAsync(
        IEnumerable<ProductionRecord> records,
        bool dryRun,
        CancellationToken cancellationToken)
    {
        var validRecords = records.Where(r => !string.IsNullOrWhiteSpace(r.Pid)).ToList();

        if (dryRun)
        {
            return new ImportSummary(validRecords.Count, 0, 0);
        }

        const int batchSize = 1000;
        for (var i = 0; i < validRecords.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = validRecords.Skip(i).Take(batchSize).ToList();
            await _repository.UpsertBatchAsync(batch, cancellationToken);
        }

        // Với cơ chế ON DUPLICATE KEY UPDATE, chúng ta không phân biệt chính xác
        // được số Insert vs Update, nên sẽ coi tất cả đã được xử lý (Processed).
        return new ImportSummary(validRecords.Count, 0, 0);
    }
}
