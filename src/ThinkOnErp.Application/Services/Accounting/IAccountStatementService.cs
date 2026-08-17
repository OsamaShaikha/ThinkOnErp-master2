using ThinkOnErp.Application.DTOs.Accounting.AccountStatement;

namespace ThinkOnErp.Application.Services.Accounting;

/// <summary>
/// Service interface for generating general ledger Account Statements (كشف حساب).
/// </summary>
public interface IAccountStatementService
{
    /// <summary>
    /// Generates a detailed Account Statement with opening balance, chronologically ordered
    /// voucher postings, running balances, and closing totals.
    /// </summary>
    Task<AccountStatementReportDto> GetAccountStatementAsync(
        AccountStatementRequestDto request,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Generates a summary ledger balance statement across multiple accounts for the period.
    /// </summary>
    Task<IReadOnlyList<AccountStatementSummaryDto>> GetAccountsSummaryAsync(
        AccountStatementRequestDto request,
        CancellationToken cancellationToken = default);
}
