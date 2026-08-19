using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlFiscalPeriodRepository
{
    Task<IReadOnlyList<GlFiscalPeriod>> GetByFiscalYearIdAsync(long fiscalYearId, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriod?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriod?> GetPeriodByDateAsync(long fiscalYearId, DateTime date, CancellationToken cancellationToken = default);
    Task<GlFiscalPeriod?> GetPeriodByDateOnlyAsync(DateTime date, CancellationToken cancellationToken = default);
    Task AddAsync(GlFiscalPeriod period, CancellationToken cancellationToken = default);
    Task AddRangeAsync(IEnumerable<GlFiscalPeriod> periods, CancellationToken cancellationToken = default);
    Task DeleteRangeAsync(IEnumerable<GlFiscalPeriod> periods, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
