using ThinkOnErp.Application.DTOs.Accounting.Banking;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IBankingService
{
    // Bank Accounts
    Task<IReadOnlyList<BankAccountDto>> GetBankAccountsAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default);
    Task<BankAccountDto> GetBankAccountByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<BankAccountDto> CreateBankAccountAsync(CreateBankAccountDto dto, string username, CancellationToken cancellationToken = default);
    Task<BankAccountDto> UpdateBankAccountAsync(long id, UpdateBankAccountDto dto, string username, CancellationToken cancellationToken = default);

    // Cash Registers
    Task<IReadOnlyList<CashRegisterDto>> GetCashRegistersAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default);
    Task<CashRegisterDto> GetCashRegisterByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<CashRegisterDto> CreateCashRegisterAsync(CreateCashRegisterDto dto, string username, CancellationToken cancellationToken = default);
    Task<CashRegisterDto> UpdateCashRegisterAsync(long id, UpdateCashRegisterDto dto, string username, CancellationToken cancellationToken = default);

    // Bank Reconciliation
    Task<BankReconciliationDto> CreateReconciliationAsync(CreateBankReconciliationDto dto, string username, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> GetReconciliationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankReconciliationDto>> GetReconciliationsAsync(long? bankAccountId, long? fiscalYearId, CancellationToken cancellationToken = default);
    Task<AutoReconcileResultDto> AutoReconcileAsync(long reconciliationId, string username, CancellationToken cancellationToken = default);
    Task<BankReconciliationDto> ManualMatchAsync(long reconciliationId, ManualMatchLineDto matchDto, string username, CancellationToken cancellationToken = default);
    Task<BankReconciliationStatementDto> GetReconciliationStatementReportAsync(long reconciliationId, CancellationToken cancellationToken = default);
}
