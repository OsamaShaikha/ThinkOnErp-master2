using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Interfaces.Inventory;

public interface IInvOpeningBalanceRepository
{
    Task<InvOpeningBatch?> GetByIdAsync(long id, CancellationToken ct = default);
    Task<InvOpeningBatch?> GetByBatchNoAsync(string batchNo, CancellationToken ct = default);
    Task<(List<InvOpeningBatch> Batches, int TotalCount)> GetAllAsync(long branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<InvOpeningBatch> CreateAsync(InvOpeningBatch batch, CancellationToken ct = default);
    Task UpdateAsync(InvOpeningBatch batch, CancellationToken ct = default);
    Task DeleteAsync(long id, CancellationToken ct = default);
}
