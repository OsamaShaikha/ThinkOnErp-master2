using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Accounting;
using ThinkOnErp.Domain.Interfaces.Accounting;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Accounting;

public sealed class PaymentMethodRepository : IPaymentMethodRepository
{
    private readonly OracleDbContext _context;

    public PaymentMethodRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PaymentMethod?> GetByIdAsync(long id, CancellationToken ct = default)
    {
        return await _context.PaymentMethods
            .Include(p => p.GlAccount)
            .Include(p => p.CommissionGlAccount)
            .Include(p => p.BankAccount)
            .Include(p => p.CashRegister)
            .FirstOrDefaultAsync(p => p.Id == id, ct);
    }

    public async Task<PaymentMethod?> GetByCodeAsync(long branchId, string code, CancellationToken ct = default)
    {
        return await _context.PaymentMethods
            .Include(p => p.GlAccount)
            .Include(p => p.CommissionGlAccount)
            .Include(p => p.BankAccount)
            .Include(p => p.CashRegister)
            .FirstOrDefaultAsync(p => p.BranchId == branchId && p.Code == code, ct);
    }

    public async Task<IReadOnlyList<PaymentMethod>> GetAllAsync(
        long? branchId,
        string? methodType,
        bool? showInPos,
        bool? showInInvoices,
        bool? activeOnly,
        CancellationToken ct = default)
    {
        var query = _context.PaymentMethods
            .Include(p => p.GlAccount)
            .Include(p => p.CommissionGlAccount)
            .Include(p => p.BankAccount)
            .Include(p => p.CashRegister)
            .AsQueryable();

        if (branchId.HasValue)
            query = query.Where(p => p.BranchId == branchId.Value);

        if (!string.IsNullOrWhiteSpace(methodType))
            query = query.Where(p => p.MethodType == methodType);

        if (showInPos.HasValue)
            query = query.Where(p => p.ShowInPos == showInPos.Value);

        if (showInInvoices.HasValue)
            query = query.Where(p => p.ShowInInvoices == showInInvoices.Value);

        if (activeOnly.HasValue && activeOnly.Value)
            query = query.Where(p => p.IsActive);

        return await query
            .OrderBy(p => p.DisplayOrder)
            .ThenBy(p => p.Code)
            .ToListAsync(ct);
    }

    public async Task AddAsync(PaymentMethod method, CancellationToken ct = default)
    {
        await _context.PaymentMethods.AddAsync(method, ct);
        await _context.SaveChangesAsync(ct);
    }

    public async Task UpdateAsync(PaymentMethod method, CancellationToken ct = default)
    {
        _context.PaymentMethods.Update(method);
        await _context.SaveChangesAsync(ct);
    }

    public async Task DeleteAsync(PaymentMethod method, CancellationToken ct = default)
    {
        _context.PaymentMethods.Remove(method);
        await _context.SaveChangesAsync(ct);
    }
}
