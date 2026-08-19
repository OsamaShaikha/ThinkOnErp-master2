using ThinkOnErp.Application.DTOs.Accounting.Subledger;

namespace ThinkOnErp.Application.Services.Accounting;

public interface ISubledgerService
{
    // Transaction Lookups
    Task<IReadOnlyList<SubledgerTransactionDto>> GetArTransactionsAsync(string? customerCode, DateTime? fromDate, DateTime? toDate, bool onlyOpen = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SubledgerTransactionDto>> GetApTransactionsAsync(string? vendorCode, DateTime? fromDate, DateTime? toDate, bool onlyOpen = false, CancellationToken cancellationToken = default);

    // Open Items Lookup (for UI Payment Matching Screen)
    Task<IReadOnlyList<OpenInvoiceDto>> GetArOpenInvoicesAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenInvoiceDto>> GetApOpenBillsAsync(string vendorCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenInvoiceDto>> GetArOpenPaymentsAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<OpenInvoiceDto>> GetApOpenPaymentsAsync(string vendorCode, CancellationToken cancellationToken = default);

    // Manual Matching / Cash Application
    Task<List<CashApplicationResultDto>> ApplyArCashAsync(ApplyCashDto dto, string username, CancellationToken cancellationToken = default);
    Task<List<CashApplicationResultDto>> ApplyApCashAsync(ApplyCashDto dto, string username, CancellationToken cancellationToken = default);

    // Automatic FIFO Matching
    Task<List<CashApplicationResultDto>> AutoApplyArCashAsync(AutoApplyCashDto dto, string username, CancellationToken cancellationToken = default);
    Task<List<CashApplicationResultDto>> AutoApplyApCashAsync(AutoApplyCashDto dto, string username, CancellationToken cancellationToken = default);

    // Unapply / Reversal of Matching
    Task<bool> UnapplyArCashAsync(long applicationId, string username, CancellationToken cancellationToken = default);
    Task<bool> UnapplyApCashAsync(long applicationId, string username, CancellationToken cancellationToken = default);

    // Application Details / Settlement History
    Task<IReadOnlyList<CashApplicationDetailDto>> GetArApplicationsByInvoiceAsync(long invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashApplicationDetailDto>> GetArApplicationsByPaymentAsync(long paymentId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashApplicationDetailDto>> GetApApplicationsByBillAsync(long billId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashApplicationDetailDto>> GetApApplicationsByPaymentAsync(long paymentId, CancellationToken cancellationToken = default);

    // Reports & Statements
    Task<StatementOfAccountDto> GetCustomerStatementAsync(string customerCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<StatementOfAccountDto> GetVendorStatementAsync(string vendorCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);

    Task<AgingReportDto> GetArAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<AgingReportDto> GetApAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default);

    Task<SubledgerReconciliationDto> ReconcileArAsync(long fiscalYearId, CancellationToken cancellationToken = default);
    Task<SubledgerReconciliationDto> ReconcileApAsync(long fiscalYearId, CancellationToken cancellationToken = default);
}
