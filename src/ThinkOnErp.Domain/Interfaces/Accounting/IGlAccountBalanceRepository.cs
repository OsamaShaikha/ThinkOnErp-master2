using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IGlAccountBalanceRepository
{
    Task<GlAccountBalance?> GetBalanceAsync(string accountCode, long branchId, long fiscalYearId, long fiscalPeriodId, long currencyId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountBalance>> GetBalancesAsync(string? accountCode, long? branchId, long fiscalYearId, long? fiscalPeriodId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlAccountBalance>> GetTrialBalanceAsync(long? branchId, long fiscalYearId, long fromPeriodId, long toPeriodId, CancellationToken cancellationToken = default);
    Task UpsertBalanceAsync(GlAccountBalance balance, CancellationToken cancellationToken = default);
    Task UpdateBalanceFromVoucherAsync(long fiscalYearId, long fiscalPeriodId, long branchId, string accountCode, long currencyId, decimal debit, decimal credit, decimal localDebit, decimal localCredit, bool isAddition, string username, CancellationToken cancellationToken = default);
    Task RecalculateAllBalancesAsync(long fiscalYearId, string username, CancellationToken cancellationToken = default);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
