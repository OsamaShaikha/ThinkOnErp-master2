using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvStockBalanceRepository
{
    Task<InvStockBalance?> GetAsync(long itemId, long warehouseId, long? binId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvStockBalance>> GetByItemAsync(long itemId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvStockBalance>> GetByWarehouseAsync(long warehouseId, CancellationToken cancellationToken = default);
    Task UpdateBalanceAsync(InvStockBalance balance, CancellationToken cancellationToken = default);
    Task<InvStockBalance> GetOrCreateAsync(long itemId, long warehouseId, long? binId = null, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
