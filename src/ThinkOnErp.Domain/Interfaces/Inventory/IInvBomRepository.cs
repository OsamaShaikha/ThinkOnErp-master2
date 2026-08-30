using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvBomRepository
{
    Task<InvBomHeader?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<InvBomHeader?> GetByCodeAsync(string code, CancellationToken ct = default);
    Task<InvBomHeader?> GetDefaultByParentItemIdAsync(long parentItemId, CancellationToken ct = default);
    Task<List<InvBomHeader>> GetAllByParentItemIdAsync(long parentItemId, CancellationToken ct = default);
    Task<(List<InvBomHeader> Items, int TotalCount)> GetPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<InvBomHeader> CreateAsync(InvBomHeader bom, CancellationToken ct = default);
    Task UpdateAsync(InvBomHeader bom, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
    Task<bool> ExistsAsync(string code, long? excludeId = null, CancellationToken ct = default);
}
