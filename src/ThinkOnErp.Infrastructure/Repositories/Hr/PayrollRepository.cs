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

public sealed class PayrollRepository : IPayrollRepository
{
    private readonly OracleDbContext _context;

    public PayrollRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<PayrollRun>> GetAllRunsAsync(
        string? payPeriod = null,
        long? branchId = null,
        string? status = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollRuns.AsNoTracking();

        if (!string.IsNullOrWhiteSpace(payPeriod))
        {
            query = query.Where(r => r.PayPeriod == payPeriod);
        }

        if (branchId.HasValue)
        {
            query = query.Where(r => r.BranchId == branchId);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(r => r.Status == status);
        }

        return await query
            .OrderByDescending(r => r.PayPeriod)
            .ThenByDescending(r => r.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollRun?> GetRunByIdAsync(long id, bool includeLines = true, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollRuns.AsQueryable();

        if (includeLines)
        {
            query = query
                .Include(r => r.Lines)
                    .ThenInclude(l => l.Components)
                .Include(r => r.Lines)
                    .ThenInclude(l => l.Employee);
        }

        return await query.SingleOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PayrollRun?> GetRunByPeriodAndBranchAsync(string payPeriod, long? branchId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRuns
            .Include(r => r.Lines)
                .ThenInclude(l => l.Components)
            .SingleOrDefaultAsync(r => r.PayPeriod == payPeriod && r.BranchId == branchId, cancellationToken);
    }

    public async Task AddRunAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(run);
        await _context.PayrollRuns.AddAsync(run, cancellationToken);
    }

    public void UpdateRun(PayrollRun run)
    {
        ArgumentNullException.ThrowIfNull(run);
        _context.PayrollRuns.Update(run);
    }

    public void RemoveRunLines(IEnumerable<PayrollRunLine> lines)
    {
        ArgumentNullException.ThrowIfNull(lines);
        _context.PayrollRunLines.RemoveRange(lines);
    }

    public async Task<IReadOnlyList<PayrollRunLine>> GetEmployeePayslipsAsync(
        string employeeCode,
        string? payPeriod = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollRunLines
            .Include(l => l.Components)
            .Include(l => l.PayrollRun)
            .Include(l => l.Employee)
            .AsNoTracking()
            .Where(l => l.EmployeeCode == employeeCode);

        if (!string.IsNullOrWhiteSpace(payPeriod))
        {
            query = query.Where(l => l.PayrollRun != null && l.PayrollRun.PayPeriod == payPeriod);
        }

        return await query
            .OrderByDescending(l => l.PayrollRun!.PayPeriod)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollRunLine?> GetEmployeePayslipForPeriodAsync(string employeeCode, string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRunLines
            .Include(l => l.Components)
            .Include(l => l.PayrollRun)
            .Include(l => l.Employee)
            .AsNoTracking()
            .SingleOrDefaultAsync(l => l.EmployeeCode == employeeCode && l.PayrollRun != null && l.PayrollRun.PayPeriod == payPeriod, cancellationToken);
    }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        return _context.SaveChangesAsync(cancellationToken);
    }
}
