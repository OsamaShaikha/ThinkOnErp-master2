using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class ApSubledgerRepository : IApSubledgerRepository
{
    private readonly OracleDbContext _context;

    public ApSubledgerRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<ApSubledgerTransaction?> GetByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.ApSubledgerTransactions
            .Include(t => t.Vendor)
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .FirstOrDefaultAsync(t => t.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApSubledgerTransaction>> GetTransactionsAsync(
        string? vendorCode,
        DateTime? fromDate,
        DateTime? toDate,
        bool onlyOpen = false,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ApSubledgerTransactions
            .Include(t => t.Vendor)
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(vendorCode))
        {
            query = query.Where(t => t.VendorCode == vendorCode);
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

    public async Task<IReadOnlyList<ApSubledgerTransaction>> GetOpenBillsAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        return await _context.ApSubledgerTransactions
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .Where(t => t.VendorCode == vendorCode && t.OpenAmount > 0)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApSubledgerTransaction>> GetOpenPaymentsAsync(string vendorCode, CancellationToken cancellationToken = default)
    {
        return await _context.ApSubledgerTransactions
            .Include(t => t.Currency)
            .Include(t => t.Voucher)
            .Where(t => t.VendorCode == vendorCode && t.OpenAmount < 0)
            .OrderBy(t => t.TransactionDate)
            .ThenBy(t => t.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task AddTransactionAsync(ApSubledgerTransaction transaction, CancellationToken cancellationToken = default)
    {
        await _context.ApSubledgerTransactions.AddAsync(transaction, cancellationToken);
    }

    public async Task<ApCashApplication?> GetApplicationByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.ApCashApplications
            .Include(a => a.PaymentTransaction)
            .Include(a => a.InvoiceTransaction)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyList<ApCashApplication>> GetApplicationsByBillIdAsync(long billId, CancellationToken cancellationToken = default)
    {
        return await _context.ApCashApplications
            .Include(a => a.PaymentTransaction)
                .ThenInclude(p => p.Voucher)
            .Where(a => a.InvoiceTransactionId == billId)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<ApCashApplication>> GetApplicationsByPaymentIdAsync(long paymentId, CancellationToken cancellationToken = default)
    {
        return await _context.ApCashApplications
            .Include(a => a.InvoiceTransaction)
                .ThenInclude(i => i.Voucher)
            .Where(a => a.PaymentTransactionId == paymentId)
            .OrderByDescending(a => a.AppliedDate)
            .ToListAsync(cancellationToken);
    }

    public async Task AddCashApplicationAsync(ApCashApplication application, CancellationToken cancellationToken = default)
    {
        await _context.ApCashApplications.AddAsync(application, cancellationToken);
    }

    public void RemoveCashApplication(ApCashApplication application)
    {
        _context.ApCashApplications.Remove(application);
    }

    public async Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return await _context.SaveChangesAsync(cancellationToken);
    }
}
