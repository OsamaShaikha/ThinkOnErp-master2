using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IArSubledgerRepository
{
    Task<ArSubledgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArSubledgerTransaction>> GetTransactionsAsync(string? customerCode, DateTime? fromDate, DateTime? toDate, bool onlyOpen = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArSubledgerTransaction>> GetOpenInvoicesAsync(string customerCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArSubledgerTransaction>> GetOpenPaymentsAsync(string customerCode, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(ArSubledgerTransaction transaction, CancellationToken cancellationToken = default);
    Task<ArCashApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArCashApplication>> GetApplicationsByInvoiceIdAsync(long invoiceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ArCashApplication>> GetApplicationsByPaymentIdAsync(long paymentId, CancellationToken cancellationToken = default);
    Task AddCashApplicationAsync(ArCashApplication application, CancellationToken cancellationToken = default);
    void RemoveCashApplication(ArCashApplication application);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
