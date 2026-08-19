using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IBankingRepository
{
    // Bank Accounts
    Task<BankAccount?> GetBankAccountByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<BankAccount?> GetBankAccountByNumberAsync(string accountNumber, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankAccount>> GetBankAccountsAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default);
    Task AddBankAccountAsync(BankAccount account, CancellationToken cancellationToken = default);

    // Cash Registers
    Task<CashRegister?> GetCashRegisterByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<CashRegister?> GetCashRegisterByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CashRegister>> GetCashRegistersAsync(long? branchId, bool? activeOnly, CancellationToken cancellationToken = default);
    Task AddCashRegisterAsync(CashRegister register, CancellationToken cancellationToken = default);

    // Bank Reconciliation
    Task<BankReconciliation?> GetReconciliationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<BankReconciliation>> GetReconciliationsAsync(long? bankAccountId, long? fiscalYearId, CancellationToken cancellationToken = default);
    Task AddReconciliationAsync(BankReconciliation reconciliation, CancellationToken cancellationToken = default);
    Task AddStatementLinesAsync(IEnumerable<BankStatementLine> lines, CancellationToken cancellationToken = default);
    Task RemoveStatementLinesAsync(long reconciliationId, CancellationToken cancellationToken = default);

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
