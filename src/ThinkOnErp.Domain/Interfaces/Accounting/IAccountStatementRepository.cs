using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public record AccountSummaryAggregate(
    string AccountCode,
    decimal OpeningDebit,
    decimal OpeningCredit,
    decimal PeriodDebit,
    decimal PeriodCredit);

public interface IAccountStatementRepository
{
    Task<GlAccount?> GetAccountByCodeAsync(
        string accountCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlAccount>> GetChildAccountsAsync(
        string parentAccountCode,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlAccount>> GetAllAccountsAsync(
        CancellationToken cancellationToken = default);

    Task<(decimal Debit, decimal Credit)> GetOpeningBalanceAsync(
        IReadOnlyList<string> accountCodes,
        DateTime beforeDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlVoucherDetail>> GetPeriodDetailsAsync(
        IReadOnlyList<string> accountCodes,
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<AccountSummaryAggregate>> GetAccountsSummaryAggregatesAsync(
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlVoucherType>> GetVoucherTypesAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GlCostCenter>> GetCostCentersAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ThinkOnErp.Domain.Entities.Views.GlAccountStatementView>> GetStatementFromViewAsync(
        IReadOnlyList<string> accountCodes,
        DateTime? fromDate,
        DateTime? toDate,
        long? branchId,
        long? fiscalYearId,
        string? costCenterCode,
        bool includeUnposted,
        CancellationToken cancellationToken = default);
}
