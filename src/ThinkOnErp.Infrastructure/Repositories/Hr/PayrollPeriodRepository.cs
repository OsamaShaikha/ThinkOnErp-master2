using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PayrollPeriodRepository : IPayrollPeriodRepository
{
    private readonly OracleDbContext _context;

    public PayrollPeriodRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<PayrollPeriod>> GetPeriodsAsync(long companyId, int? fiscalYear = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollPeriods
            .Where(p => p.CompanyId == companyId);

        if (fiscalYear.HasValue)
            query = query.Where(p => p.FiscalYear == fiscalYear.Value);

        return await query.OrderByDescending(p => p.StartDate).ToListAsync(cancellationToken);
    }

    public async Task<PayrollPeriod?> GetPeriodByCodeAsync(long companyId, string periodCode, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollPeriods
            .FirstOrDefaultAsync(p => p.CompanyId == companyId && p.PeriodCode == periodCode, cancellationToken);
    }

    public async Task AddPeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
    {
        await _context.PayrollPeriods.AddAsync(period, cancellationToken);
    }

    public Task UpdatePeriodAsync(PayrollPeriod period, CancellationToken cancellationToken = default)
    {
        _context.PayrollPeriods.Update(period);
        return Task.CompletedTask;
    }

    public async Task<PayrollRun?> GetPayrollRunByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRuns
            .Include(r => r.Lines)
                .ThenInclude(l => l.Components)
            .Include(r => r.Lines)
                .ThenInclude(l => l.Snapshot)
            .FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    public async Task<PayrollRun?> GetPayrollRunByPeriodAsync(string payPeriod, long? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.PayrollRuns
            .Include(r => r.Lines)
                .ThenInclude(l => l.Components)
            .Where(r => r.PayPeriod == payPeriod);

        if (branchId.HasValue)
            query = query.Where(r => r.BranchId == branchId.Value);

        return await query.FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddPayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        await _context.PayrollRuns.AddAsync(run, cancellationToken);
    }

    public Task UpdatePayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        _context.PayrollRuns.Update(run);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<PayrollRunLine>> GetRunLinesAsync(long payrollRunId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRunLines
            .Include(l => l.Components)
            .Include(l => l.Employee)
            .Where(l => l.PayrollRunId == payrollRunId)
            .ToListAsync(cancellationToken);
    }

    public async Task<PayrollRunLine?> GetRunLineWithSnapshotAsync(long runLineId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRunLines
            .Include(l => l.Components)
            .Include(l => l.Snapshot)
            .Include(l => l.Employee)
            .FirstOrDefaultAsync(l => l.Id == runLineId, cancellationToken);
    }

    public async Task<PayrollCalculationSnapshot?> GetSnapshotByRunLineIdAsync(long runLineId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollCalculationSnapshots
            .FirstOrDefaultAsync(s => s.PayrollRunLineId == runLineId, cancellationToken);
    }

    public async Task<IReadOnlyList<Employee>> GetActiveEmployeesAsync(long? branchId = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Employees
            .Include(e => e.SalaryStructures.Where(s => s.IsActive))
                .ThenInclude(s => s.Lines.Where(l => l.IsActive))
                    .ThenInclude(l => l.Component)
            .Include(e => e.Dependents.Where(d => d.IsActive))
            .Where(e => e.IsActive && e.EmploymentStatus == "ACTIVE");

        if (branchId.HasValue)
            query = query.Where(e => e.BranchId == branchId.Value);

        return await query.ToListAsync(cancellationToken);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
