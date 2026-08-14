using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlCostCenterRepository
{
    Task<IReadOnlyList<GlCostCenter>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<GlCostCenter?> GetByCodeAsync(string costCenterCode, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string costCenterCode, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(string costCenterCode, CancellationToken cancellationToken = default);
    Task AddAsync(GlCostCenter costCenter, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
