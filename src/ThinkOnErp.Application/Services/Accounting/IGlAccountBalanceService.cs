using ThinkOnErp.Application.DTOs.Accounting.Balances;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlAccountBalanceService
{
    Task<IReadOnlyList<GlAccountBalanceDto>> GetBalancesAsync(AccountBalanceFilterDto filter, CancellationToken cancellationToken = default);
    Task<TrialBalanceReportDto> GetTrialBalanceAsync(long fiscalYearId, long fromPeriodId, long toPeriodId, long? branchId, CancellationToken cancellationToken = default);
    Task RecalculateBalancesAsync(long fiscalYearId, string username, CancellationToken cancellationToken = default);
}
