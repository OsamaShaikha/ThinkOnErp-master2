using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class ExpenseClaimRepository : IExpenseClaimRepository
{
    private readonly OracleDbContext _context;

    public ExpenseClaimRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<ExpenseClaim>> GetAllClaimsAsync(
        string? employeeCode = null,
        string? status = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.ExpenseClaims
            .Include(c => c.Employee)
            .Include(c => c.Lines)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(c => c.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(c => c.Status == status);
        }

        if (fromDate.HasValue)
        {
            query = query.Where(c => c.ClaimDate >= fromDate.Value.Date);
        }

        if (toDate.HasValue)
        {
            query = query.Where(c => c.ClaimDate <= toDate.Value.Date);
        }

        return await query.OrderByDescending(c => c.ClaimDate).ToListAsync(cancellationToken);
    }

    public async Task<ExpenseClaim?> GetClaimByIdAsync(long id, bool includeLines = true, CancellationToken cancellationToken = default)
    {
        var query = _context.ExpenseClaims
            .Include(c => c.Employee)
            .AsQueryable();

        if (includeLines)
        {
            query = query.Include(c => c.Lines);
        }

        return await query.SingleOrDefaultAsync(c => c.Id == id, cancellationToken);
    }

    public async Task<ExpenseClaim?> GetClaimByNumberAsync(string claimNumber, CancellationToken cancellationToken = default)
    {
        return await _context.ExpenseClaims
            .Include(c => c.Lines)
            .SingleOrDefaultAsync(c => c.ClaimNumber == claimNumber, cancellationToken);
    }

    public async Task AddClaimAsync(ExpenseClaim claim, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(claim);
        await _context.ExpenseClaims.AddAsync(claim, cancellationToken);
    }

    public void UpdateClaim(ExpenseClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        _context.ExpenseClaims.Update(claim);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
