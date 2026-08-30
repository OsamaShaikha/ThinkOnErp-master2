using ThinkOnErp.Domain.Entities.Accounting;

namespace ThinkOnErp.Domain.Interfaces.Accounting;

public interface ITaxRepository
{
    // Tax Categories
    Task<IReadOnlyList<TaxCategory>> GetTaxCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxCategory?> GetTaxCategoryByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxCategory?> GetTaxCategoryByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddTaxCategoryAsync(TaxCategory category, CancellationToken cancellationToken = default);
    Task DeleteTaxCategoryAsync(TaxCategory category, CancellationToken cancellationToken = default);

    // Tax Rates
    Task<IReadOnlyList<TaxRate>> GetTaxRatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxRate?> GetTaxRateByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxRate?> GetTaxRateByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddTaxRateAsync(TaxRate rate, CancellationToken cancellationToken = default);
    Task DeleteTaxRateAsync(TaxRate rate, CancellationToken cancellationToken = default);

    // Tax Groups
    Task<IReadOnlyList<TaxGroup>> GetTaxGroupsAsync(bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<TaxGroup?> GetTaxGroupByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<TaxGroup?> GetTaxGroupByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task AddTaxGroupAsync(TaxGroup group, CancellationToken cancellationToken = default);
    Task DeleteTaxGroupAsync(TaxGroup group, CancellationToken cancellationToken = default);

    // Tax Transactions & Declarations
    Task AddTaxTransactionAsync(TaxTransaction transaction, CancellationToken cancellationToken = default);
    Task AddTaxTransactionsAsync(IEnumerable<TaxTransaction> transactions, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<TaxTransaction>> GetTaxTransactionsAsync(
        long? branchId,
        DateTime fromDate,
        DateTime toDate,
        bool? isSalesTax = null,
        CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
