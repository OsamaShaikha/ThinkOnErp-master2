using ThinkOnErp.Application.DTOs.Accounting.Reports;
using ThinkOnErp.Application.DTOs.Accounting.Subledger;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IFinancialReportsService
{
    Task<GlStatementDto> GetGeneralLedgerStatementAsync(GlStatementFilterDto filter, CancellationToken cancellationToken = default);
    Task<IncomeStatementDto> GetIncomeStatementAsync(FinancialStatementFilterDto filter, CancellationToken cancellationToken = default);
    Task<BalanceSheetDto> GetBalanceSheetAsync(FinancialStatementFilterDto filter, CancellationToken cancellationToken = default);

    // Forwarding subledger statements for centralized reporting hub
    Task<StatementOfAccountDto> GetCustomerStatementAsync(string customerCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<StatementOfAccountDto> GetVendorStatementAsync(string vendorCode, DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default);
    Task<AgingReportDto> GetArAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<AgingReportDto> GetApAgingReportAsync(DateTime asOfDate, CancellationToken cancellationToken = default);
}
