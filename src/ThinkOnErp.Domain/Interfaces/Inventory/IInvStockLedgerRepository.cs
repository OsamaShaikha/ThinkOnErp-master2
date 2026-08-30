using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvStockLedgerRepository
{
    Task AddEntryAsync(InvStockLedgerEntry entry, CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByItemAsync(
        long itemId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByWarehouseAsync(
        long warehouseId,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task<(IReadOnlyList<InvStockLedgerEntry> Entries, long TotalCount)> GetByDateRangeAsync(
        DateTime fromDate,
        DateTime toDate,
        int pageIndex,
        int pageSize,
        CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
