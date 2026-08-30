using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvCostLayerRepository
{
    Task AddLayerAsync(InvCostLayer layer, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvCostLayer>> GetAvailableLayersAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default);
    Task UpdateRemainingQtyAsync(long layerId, decimal quantity, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
