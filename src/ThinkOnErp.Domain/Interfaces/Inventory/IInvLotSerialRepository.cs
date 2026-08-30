using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvLotSerialRepository
{
    Task<InvLotMaster?> GetLotByIdAsync(long lotId, CancellationToken cancellationToken = default);
    Task<InvLotMaster?> GetByLotNumberAsync(long itemId, string lotNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvLotMaster>> GetAvailableLotsFefoAsync(long itemId, CancellationToken cancellationToken = default);
    Task AddLotAsync(InvLotMaster lot, CancellationToken cancellationToken = default);
    Task UpdateLotAsync(InvLotMaster lot, CancellationToken cancellationToken = default);

    Task<InvSerialMaster?> GetSerialByIdAsync(long serialId, CancellationToken cancellationToken = default);
    Task<InvSerialMaster?> GetBySerialNumberAsync(long itemId, string serialNumber, CancellationToken cancellationToken = default);
    Task AddSerialAsync(InvSerialMaster serial, CancellationToken cancellationToken = default);
    Task UpdateSerialAsync(InvSerialMaster serial, CancellationToken cancellationToken = default);
    
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
