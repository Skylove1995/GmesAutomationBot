using GmesImporter.Core.Importing;
using GmesImporter.Core.Models;

namespace GmesImporter.Tests;

public sealed class ProductionImportServiceTests
{
    [Fact]
    public async Task ImportAsync_upserts_valid_records_in_batches()
    {
        var repository = new InMemoryProductionRepository(new[]
        {
            new ExistingProductionRecord("PID-SKIP", new DateTime(2026, 4, 28, 8, 0, 0)),
            new ExistingProductionRecord("PID-UPDATE", new DateTime(2026, 4, 28, 9, 0, 0))
        });
        var service = new ProductionImportService(repository);
        var records = new[]
        {
            new ProductionRecord("WO-NEW",    "EBR-NEW",    "PID-NEW",    new DateTime(2026, 4, 28, 10, 0, 0)),
            new ProductionRecord("WO-SKIP",   "EBR-SKIP",   "PID-SKIP",   new DateTime(2026, 4, 28,  8, 0, 0)),
            new ProductionRecord("WO-UPDATE", "EBR-UPDATE", "PID-UPDATE", new DateTime(2026, 4, 28,  9, 5, 0))
        };

        var result = await service.ImportAsync(records, dryRun: false, CancellationToken.None);

        Assert.Equal(3, result.Inserted);
        Assert.Equal(0, result.Updated);
        Assert.Equal(0, result.Skipped);
        Assert.Contains(repository.Inserted, r => r.Pid == "PID-NEW");
        Assert.Contains(repository.Inserted, r => r.Pid == "PID-SKIP");
        Assert.Contains(repository.Inserted, r => r.Pid == "PID-UPDATE");
    }

    [Fact]
    public async Task ImportAsync_dry_run_counts_actions_without_writing()
    {
        var repository = new InMemoryProductionRepository(Array.Empty<ExistingProductionRecord>());
        var service = new ProductionImportService(repository);
        var records = new[]
        {
            new ProductionRecord("WO", "EBR", "PID", null)
        };

        var result = await service.ImportAsync(records, dryRun: true, CancellationToken.None);

        Assert.Equal(1, result.Inserted);
        Assert.Empty(repository.Inserted);
    }

    private sealed class InMemoryProductionRepository : IProductionRepository
    {
        private readonly Dictionary<string, ExistingProductionRecord> _existing;

        public InMemoryProductionRepository(IEnumerable<ExistingProductionRecord> existing)
        {
            _existing = existing.ToDictionary(r => r.Pid, StringComparer.OrdinalIgnoreCase);
        }

        public List<ProductionRecord> Inserted { get; } = new();

        public Task<ExistingProductionRecord?> FindByPidAsync(string pid, CancellationToken cancellationToken)
        {
            _existing.TryGetValue(pid, out var record);
            return Task.FromResult(record);
        }

        public Task InsertAsync(ProductionRecord record, CancellationToken cancellationToken)
        {
            Inserted.Add(record);
            _existing[record.Pid] = new ExistingProductionRecord(record.Pid, record.Date);
            return Task.CompletedTask;
        }

        public Task<int> UpsertBatchAsync(IReadOnlyList<ProductionRecord> batch, CancellationToken cancellationToken)
        {
            Inserted.AddRange(batch);
            foreach (var record in batch)
            {
                _existing[record.Pid] = new ExistingProductionRecord(record.Pid, record.Date);
            }
            return Task.FromResult(batch.Count);
        }
    }
}
