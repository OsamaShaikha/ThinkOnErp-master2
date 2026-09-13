using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosTillRepository
{
    Task<IReadOnlyList<PosTill>> GetTillsByBranchAsync(long branchId, bool? activeOnly = null, CancellationToken ct = default);
    Task<PosTill?> GetTillByIdAsync(long id, CancellationToken ct = default);
    Task<PosTill?> GetByCodeAsync(long branchId, string tillCode, CancellationToken ct = default);
    Task AddTillAsync(PosTill till, CancellationToken ct = default);
    Task UpdateTillAsync(PosTill till, CancellationToken ct = default);
    Task DeleteTillAsync(PosTill till, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
