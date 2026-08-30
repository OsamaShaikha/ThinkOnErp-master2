using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class TaxRepository : ITaxRepository
{
    private readonly OracleDbContext _context;

    public TaxRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Categories
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxCategory>> GetTaxCategoriesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.TaxCategories.AsNoTracking();
        if (!includeInactive)
        {
            query = query.Where(c => c.IsActive);
        }
        return await query.OrderBy(c => c.DisplayOrder).ThenBy(c => c.CategoryCode).ToListAsync(cancellationToken);
    }

    public async Task<TaxCategory?> GetTaxCategoryByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.TaxCategories
            .Include(c => c.TaxRates)
            .FirstOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<TaxCategory?> GetTaxCategoryByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.TaxCategories
            .Include(c => c.TaxRates)
            .FirstOrDefaultAsync(c => c.CategoryCode == code.Trim().ToUpper(), cancellationToken);
    }

    public async Task AddTaxCategoryAsync(TaxCategory category, CancellationToken cancellationToken = default)
    {
        await _context.TaxCategories.AddAsync(category, cancellationToken);
    }

    public Task DeleteTaxCategoryAsync(TaxCategory category, CancellationToken cancellationToken = default)
    {
        _context.TaxCategories.Remove(category);
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Rates
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxRate>> GetTaxRatesAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.TaxRates
            .Include(r => r.Category)
            .Include(r => r.SalesTaxGlAccount)
            .Include(r => r.PurchaseTaxGlAccount)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(r => r.IsActive);
        }
        return await query.OrderBy(r => r.DisplayOrder).ThenBy(r => r.TaxRateCode).ToListAsync(cancellationToken);
    }

    public async Task<TaxRate?> GetTaxRateByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.TaxRates
            .Include(r => r.Category)
            .Include(r => r.SalesTaxGlAccount)
            .Include(r => r.PurchaseTaxGlAccount)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<TaxRate?> GetTaxRateByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.TaxRates
            .Include(r => r.Category)
            .Include(r => r.SalesTaxGlAccount)
            .Include(r => r.PurchaseTaxGlAccount)
            .FirstOrDefaultAsync(r => r.TaxRateCode == code.Trim().ToUpper(), cancellationToken);
    }

    public async Task AddTaxRateAsync(TaxRate rate, CancellationToken cancellationToken = default)
    {
        await _context.TaxRates.AddAsync(rate, cancellationToken);
    }

    public Task DeleteTaxRateAsync(TaxRate rate, CancellationToken cancellationToken = default)
    {
        _context.TaxRates.Remove(rate);
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Groups
    // ─────────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TaxGroup>> GetTaxGroupsAsync(bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        var query = _context.TaxGroups
            .Include(g => g.Items)
                .ThenInclude(i => i.TaxRate)
            .AsNoTracking();

        if (!includeInactive)
        {
            query = query.Where(g => g.IsActive);
        }
        return await query.OrderBy(g => g.GroupCode).ToListAsync(cancellationToken);
    }

    public async Task<TaxGroup?> GetTaxGroupByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.TaxGroups
            .Include(g => g.Items.OrderBy(i => i.ApplicationOrder))
                .ThenInclude(i => i.TaxRate)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);
    }

    public async Task<TaxGroup?> GetTaxGroupByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        return await _context.TaxGroups
            .Include(g => g.Items.OrderBy(i => i.ApplicationOrder))
                .ThenInclude(i => i.TaxRate)
            .FirstOrDefaultAsync(g => g.GroupCode == code.Trim().ToUpper(), cancellationToken);
    }

    public async Task AddTaxGroupAsync(TaxGroup group, CancellationToken cancellationToken = default)
    {
        await _context.TaxGroups.AddAsync(group, cancellationToken);
    }

    public Task DeleteTaxGroupAsync(TaxGroup group, CancellationToken cancellationToken = default)
    {
        _context.TaxGroups.Remove(group);
        return Task.CompletedTask;
    }

    // ─────────────────────────────────────────────────────────────────────────
    // Tax Transactions
    // ─────────────────────────────────────────────────────────────────────────

    public async Task AddTaxTransactionAsync(TaxTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.TaxTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task AddTaxTransactionsAsync(IEnumerable<TaxTransaction> transactions, CancellationToken cancellationToken = default)
    {
        await _context.TaxTransactions.AddRangeAsync(transactions, cancellationToken);
    }

    public async Task<IReadOnlyList<TaxTransaction>> GetTaxTransactionsAsync(
        long? branchId,
        DateTime fromDate,
        DateTime toDate,
        bool? isSalesTax = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.TaxTransactions
            .Include(t => t.TaxRate)
            .AsNoTracking()
            .Where(t => t.TaxDate >= fromDate && t.TaxDate <= toDate);

        if (branchId.HasValue && branchId.Value > 0)
        {
            query = query.Where(t => t.BranchId == branchId.Value);
        }

        if (isSalesTax.HasValue)
        {
            query = query.Where(t => t.IsSalesTax == isSalesTax.Value);
        }

        return await query.OrderBy(t => t.TaxDate).ThenBy(t => t.Id).ToListAsync(cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
