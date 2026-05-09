using GmesImporter.Core.Models;

namespace GmesImporter.Core.Importing;

public interface IProductionRepository
{
    Task<ExistingProductionRecord?> FindByPidAsync(string pid, CancellationToken cancellationToken);

    Task InsertAsync(ProductionRecord record, CancellationToken cancellationToken);

    Task<int> UpsertBatchAsync(IReadOnlyList<ProductionRecord> batch, CancellationToken cancellationToken);
}
