using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlOpeningBalanceRepository
{
    Task<GlOpeningBalanceHeader?> GetByIdAsync(long id, CancellationToken cancellationToken = default);

    Task<GlOpeningBalanceHeader?> GetByBranchAndFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlOpeningBalanceHeader>> GetAllByBranchAsync(
        long branchId,
        CancellationToken cancellationToken = default);

    Task<bool> ExistsForFiscalYearAsync(
        long branchId,
        long fiscalYearId,
        CancellationToken cancellationToken = default);

    Task AddAsync(GlOpeningBalanceHeader header, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
