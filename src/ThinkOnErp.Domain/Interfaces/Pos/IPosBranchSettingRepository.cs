using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Pos;

namespace ThinkOnErp.Domain.Interfaces.Pos;

public interface IPosBranchSettingRepository
{
    Task<PosBranchSetting?> GetByBranchIdAsync(long branchId, CancellationToken ct = default);
    Task<IReadOnlyList<PosBranchSetting>> GetAllAsync(CancellationToken ct = default);
    Task AddAsync(PosBranchSetting setting, CancellationToken ct = default);
    Task UpdateAsync(PosBranchSetting setting, CancellationToken ct = default);
    Task SaveChangesAsync(CancellationToken ct = default);
}
