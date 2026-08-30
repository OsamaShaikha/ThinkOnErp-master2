using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvReservationRepository
{
    Task<InvReservation?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvReservation>> GetActiveByItemAsync(long itemId, long warehouseId, CancellationToken cancellationToken = default);
    Task AddAsync(InvReservation reservation, CancellationToken cancellationToken = default);
    Task FulfillAsync(long id, decimal fulfilledQty, CancellationToken cancellationToken = default);
    Task CancelAsync(long id, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
