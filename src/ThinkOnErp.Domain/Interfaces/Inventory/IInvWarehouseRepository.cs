using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvWarehouseRepository
{
    Task<InvWarehouse?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<InvWarehouse?> GetByCodeAsync(string warehouseCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InvWarehouse>> GetAllByBranchAsync(long branchId, CancellationToken cancellationToken = default);
    Task AddAsync(InvWarehouse warehouse, CancellationToken cancellationToken = default);
    Task UpdateAsync(InvWarehouse warehouse, CancellationToken cancellationToken = default);
    Task DeleteAsync(long id, CancellationToken cancellationToken = default);
    Task<InvZone?> GetZoneByIdAsync(long zoneId, CancellationToken cancellationToken = default);
    Task UpdateZoneAsync(InvZone zone, CancellationToken cancellationToken = default);
    Task DeleteZoneAsync(long zoneId, CancellationToken cancellationToken = default);
    Task<InvBin?> GetBinByIdAsync(long binId, CancellationToken cancellationToken = default);
    Task UpdateBinAsync(InvBin bin, CancellationToken cancellationToken = default);
    Task DeleteBinAsync(long binId, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
