using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class ArSubledgerRepository : IArSubledgerRepository
{
    private readonly OracleDbContext _context;

    public ArSubledgerRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<ArSubledgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.ArSubledgerTransactions
            .Include(t => t.Customer)
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArSubledgerTransaction>> GetTransactionsAsync(
        string? customerCode,
        DateTime? fromDate,
        DateTime? toDate,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ArSubledgerTransactions
            .Include(t => t.Customer)
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(customerCode))
        {
            query = query.Where(t => t.CustomerCode == customerCode);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate >= fromDate.Value);
        }

        if (toDate.HasValue)
        {
            query = query.Where(t => t.TransactionDate <= toDate.Value);
        }

        if (onlyOpen)
        {
            query = query.Where(t => t.OpenAmount != 0);
        }

        return await query.OrderBy(t => t.TransactionDate).ThenBy(t => t.Id).ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArSubledgerTransaction>> GetOpenInvoicesAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        return await _context.ArSubledgerTransactions
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .Where(t => t.CustomerCode == customerCode && t.OpenAmount > 0)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArSubledgerTransaction>> GetOpenPaymentsAsync(string customerCode, CancellationToken cancellationToken = default)
    {
        return await _context.ArSubledgerTransactions
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .Where(t => t.CustomerCode == customerCode && t.OpenAmount < 0)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTransactionAsync(ArSubledgerTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.ArSubledgerTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task<ArCashApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.ArCashApplications
            .Include(a => a.PaymentTransaction)
            .Include(a => a.InvoiceTransaction)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ArCashApplication>> GetApplicationsByInvoiceIdAsync(long invoiceId, CancellationToken cancellationToken = default)
    {
        return await _context.ArCashApplications
            .Include(a => a.PaymentTransaction)
                .ThenInclude(p => p.Voucher)
            .Where(a => a.InvoiceTransactionId == invoiceId)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ArCashApplication>> GetApplicationsByPaymentIdAsync(long paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.ArCashApplications
            .Include(a => a.InvoiceTransaction)
                .ThenInclude(i => i.Voucher)
            .Where(a => a.PaymentTransactionId == paymentId)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddCashApplicationAsync(ArCashApplication application, CancellationToken cancellationToken = default)
    {
        await _context.ArCashApplications.AddAsync(application, cancellationToken);
    }

    public void RemoveCashApplication(ArCashApplication application)
    {
        _context.ArCashApplications.Remove(application);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
