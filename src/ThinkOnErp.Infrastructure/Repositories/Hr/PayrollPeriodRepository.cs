using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;
using ThinkOnErp.Infrastructure.Services.Hr;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class PayrollPeriodRepository : IPayrollPeriodRepository
{
    public Task<PayrollRun?> GetPayrollRunSnapshotAsync(long id, CancellationToken cancellationToken = default)
        => _context.PayrollRuns.AsNoTracking().FirstOrDefaultAsync(r => r.Id == id, cancellationToken);

    public Task<PayrollRun?> GetLatestPayrollCorrectionAsync(long rootRunId, CancellationToken cancellationToken = default)
        => _context.PayrollRuns.Where(r => r.Id == rootRunId || r.CorrectionRootRunId == rootRunId)
            .OrderByDescending(r => r.CorrectionVersion).ThenByDescending(r => r.Id).FirstOrDefaultAsync(cancellationToken);

    public async Task SupersedePayrollCorrectionFamilyAsync(long rootRunId, string user, CancellationToken cancellationToken = default)
    {
        var family = await _context.PayrollRuns.Where(r => r.Id == rootRunId || r.CorrectionRootRunId == rootRunId)
            .Where(r => r.Status != "CANCELLED" && r.Status != "SUPERSEDED").ToListAsync(cancellationToken);
        foreach (var run in family)
        {
            run.Status = "SUPERSEDED";
            run.UpdateUser = user;
            run.UpdateDate = DateTime.UtcNow;
        }
    }

    public Task<bool> HasOtherActivePayrollRunAsync(string payPeriod, long? branchId, long excludingId, CancellationToken cancellationToken = default)
        => _context.PayrollRuns.AnyAsync(r => r.PayPeriod == payPeriod && r.BranchId == branchId && r.Id != excludingId && r.Status != "CANCELLED" && r.Status != "SUPERSEDED", cancellationToken);

    public async Task<bool> HasPayrollFinancialLinksAsync(long payrollRunId, CancellationToken cancellationToken = default)
    {
        var ids = _context.PayrollRunLines.Where(l => l.PayrollRunId == payrollRunId).Select(l => (long?)l.Id);
        return await _context.LoanRepaymentSchedules.AnyAsync(s => s.PayrollRunLineId.HasValue && ids.Contains(s.PayrollRunLineId), cancellationToken)
            || await _context.PayrollAdjustments.AnyAsync(a => a.PayrollRunLineId.HasValue && ids.Contains(a.PayrollRunLineId), cancellationToken);
    }
    private readonly OracleDbContext _context;

    public PayrollPeriodRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<PayrollPeriod?> GetPeriodForRunAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        var companyId = await _context.SysBranches.Where(b => b.Id == run.BranchId).Select(b => (long?)b.CompanyId).SingleOrDefaultAsync(cancellationToken);
        return companyId.HasValue ? await GetPeriodByCodeAsync(companyId.Value, run.PayPeriod, cancellationToken) : null;
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

    public async Task<IReadOnlyList<PayrollRun>> GetRunsByPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default)
    {
        var branchIds = await _context.SysBranches.Where(b => b.CompanyId == companyId).Select(b => b.Id).ToListAsync(cancellationToken);
        var query = _context.PayrollRuns
            .Include(r => r.Lines)
                .ThenInclude(l => l.Components)
            .Where(r => r.PayPeriod == payPeriod);
        if (branchIds.Count > 0)
        {
            query = query.Where(r => !r.BranchId.HasValue || branchIds.Contains(r.BranchId.Value));
        }
        return await query.ToListAsync(cancellationToken);
    }

    public async Task<(IReadOnlyList<PayrollRun> Items, long TotalCount)> GetPayrollRunsAsync(
        long companyId, string? payPeriod, long? branchId, string? status, int pageIndex, int pageSize,
        CancellationToken cancellationToken = default)
    {
        var query = from run in _context.PayrollRuns
                    join period in _context.PayrollPeriods
                        on new { CompanyId = companyId, PeriodCode = run.PayPeriod }
                        equals new { period.CompanyId, period.PeriodCode }
                    where !run.BranchId.HasValue || _context.SysBranches.Any(branch =>
                        branch.Id == run.BranchId.Value && branch.CompanyId == companyId)
                    select run;

        if (!string.IsNullOrWhiteSpace(payPeriod))
            query = query.Where(run => run.PayPeriod == payPeriod);
        if (branchId.HasValue)
            query = query.Where(run => run.BranchId == branchId.Value);
        if (!string.IsNullOrWhiteSpace(status))
            query = query.Where(run => run.Status == status);

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query
            .AsNoTracking()
            .Include(run => run.Lines)
            .OrderByDescending(run => run.RunDate)
            .ThenByDescending(run => run.Id)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public async Task<(IReadOnlyList<PayrollRunLine> Items, long TotalCount)> GetEmployeePayslipsAsync(
        long companyId, string employeeCode, int pageIndex, int pageSize, CancellationToken cancellationToken = default)
    {
        var query = from line in _context.PayrollRunLines
                    join run in _context.PayrollRuns on line.PayrollRunId equals run.Id
                    join period in _context.PayrollPeriods
                        on new { CompanyId = companyId, PeriodCode = run.PayPeriod }
                        equals new { period.CompanyId, period.PeriodCode }
                    where line.EmployeeCode == employeeCode &&
                          (run.Status == "APPROVED" || run.Status == "POSTED_TO_GL" || run.Status == "PAID")
                    select line;

        var total = await query.LongCountAsync(cancellationToken);
        var items = await query.AsNoTracking()
            .Include(line => line.PayrollRun)
            .Include(line => line.Employee)
            .OrderByDescending(line => line.PayrollRun!.RunDate)
            .ThenByDescending(line => line.PayrollRunId)
            .Skip((pageIndex - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);
        return (items, total);
    }

    public Task<PayrollRunLine?> GetEmployeePayslipAsync(
        long companyId, long payrollRunId, string employeeCode, CancellationToken cancellationToken = default)
        => (from line in _context.PayrollRunLines
            join run in _context.PayrollRuns on line.PayrollRunId equals run.Id
            join period in _context.PayrollPeriods
                on new { CompanyId = companyId, PeriodCode = run.PayPeriod }
                equals new { period.CompanyId, period.PeriodCode }
            where line.PayrollRunId == payrollRunId && line.EmployeeCode == employeeCode &&
                  (run.Status == "APPROVED" || run.Status == "POSTED_TO_GL" || run.Status == "PAID")
            select line)
            .AsNoTracking()
            .Include(line => line.PayrollRun)
            .Include(line => line.Employee)
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<bool> HasPendingAdjustmentsForPeriodAsync(long companyId, string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollAdjustments
            .AnyAsync(a => a.CompanyId == companyId && a.PayPeriod == payPeriod && a.Status == "PENDING", cancellationToken);
    }

    public async Task<bool> HasSubsequentClosedPeriodsAsync(long companyId, int fiscalYear, int month, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollPeriods
            .AnyAsync(p => p.CompanyId == companyId && p.Status == "CLOSED" &&
                (p.FiscalYear > fiscalYear || (p.FiscalYear == fiscalYear && p.Month > month)), cancellationToken);
    }

    public async Task<PayrollRun?> GetPreviousPayrollRunAsync(long? branchId, string currentPayPeriod, long currentRunId, CancellationToken cancellationToken = default)
    {
        return await _context.PayrollRuns
            .Include(r => r.Lines)
                .ThenInclude(l => l.Components)
            .Where(r => r.BranchId == branchId && r.Id != currentRunId && r.Status != "CANCELLED" && r.Status != "SUPERSEDED")
            .OrderByDescending(r => r.Id)
            .FirstOrDefaultAsync(cancellationToken);
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
            .Where(r => r.PayPeriod == payPeriod && r.Status != "CANCELLED" && r.Status != "SUPERSEDED");

        if (branchId.HasValue)
            query = query.Where(r => r.BranchId == branchId.Value);

        return await query.OrderByDescending(r => r.Id).FirstOrDefaultAsync(cancellationToken);
    }

    public async Task AddPayrollRunAsync(PayrollRun run, CancellationToken cancellationToken = default)
    {
        await _context.PayrollRuns.AddAsync(run, cancellationToken);
    }

    public async Task LinkApprovedAdjustmentsToRunAsync(long companyId, PayrollRun run, string user, CancellationToken cancellationToken = default)
    {
        var runLines = run.Lines.Where(line => line.Id > 0).ToDictionary(line => line.EmployeeCode, line => line.Id, StringComparer.Ordinal);
        if (runLines.Count == 0) return;
        var employeeCodes = runLines.Keys.ToArray();
        var adjustments = await PayrollCalculationAdjustmentQuery.ForPeriod(_context, companyId, run.PayPeriod)
            .Where(adjustment => employeeCodes.Contains(adjustment.EmployeeCode))
            .ToListAsync(cancellationToken);
        var priorRunLineIds = adjustments.Where(adjustment => adjustment.PayrollRunLineId.HasValue &&
                runLines[adjustment.EmployeeCode] != adjustment.PayrollRunLineId.Value)
            .Select(adjustment => adjustment.PayrollRunLineId!.Value).Distinct().ToArray();
        var priorRunStatuses = await _context.PayrollRunLines.Where(line => priorRunLineIds.Contains(line.Id))
            .Select(line => new { line.Id, Status = line.PayrollRun!.Status })
            .ToDictionaryAsync(line => line.Id, line => line.Status, cancellationToken);
        foreach (var adjustment in adjustments)
        {
            var targetLineId = runLines[adjustment.EmployeeCode];
            if (adjustment.PayrollRunLineId == targetLineId) continue;
            if (adjustment.PayrollRunLineId.HasValue)
            {
                if (!priorRunStatuses.TryGetValue(adjustment.PayrollRunLineId.Value, out var priorStatus) || priorStatus is not ("CANCELLED" or "SUPERSEDED"))
                    throw new InvalidOperationException("An approved payroll adjustment is already linked to another active payroll run.");
            }
            adjustment.PayrollRunLineId = targetLineId;
            adjustment.UpdateUser = user;
            adjustment.UpdateDate = DateTime.UtcNow;
        }
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
