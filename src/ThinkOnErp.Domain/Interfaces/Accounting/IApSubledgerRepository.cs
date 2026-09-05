using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface IApSubledgerRepository
{
    Task<ApSubledgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApSubledgerTransaction>> GetTransactionsAsync(string? vendorCode, DateTime? fromDate, DateTime? toDate, bool onlyOpen = false, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApSubledgerTransaction>> GetOpenBillsAsync(string vendorCode, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApSubledgerTransaction>> GetOpenPaymentsAsync(string vendorCode, CancellationToken cancellationToken = default);
    Task AddTransactionAsync(ApSubledgerTransaction transaction, CancellationToken cancellationToken = default);
    Task<ApCashApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApCashApplication>> GetApplicationsByBillIdAsync(long billId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ApCashApplication>> GetApplicationsByPaymentIdAsync(long paymentId, CancellationToken cancellationToken = default);
    Task AddCashApplicationAsync(ApCashApplication application, CancellationToken cancellationToken = default);
    void RemoveCashApplication(ApCashApplication application);
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ThinkOnErp.Domain.Entities.Views.ApAgingAnalysisView>> GetAgingAnalysisFromViewAsync(long? branchId, DateTime asOfDate, CancellationToken cancellationToken = default);
}
