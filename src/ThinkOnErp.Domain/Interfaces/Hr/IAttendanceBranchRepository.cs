using ThinkOnErp.Domain.Entities;
namespace ThinkOnErp.Domain.Interfaces.Hr;

// HR-specific branch reads must never use the unscoped ERP branch catalog.
public interface IAttendanceBranchRepository
{
    Task<SysBranch?> GetAsync(long id, CancellationToken ct = default);
    Task<IReadOnlyList<SysBranch>> ListAsync(long? companyId, CancellationToken ct = default);
}
