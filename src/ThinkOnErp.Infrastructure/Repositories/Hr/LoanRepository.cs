using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using ThinkOnErp.Domain.Entities.Hr;
using ThinkOnErp.Domain.Interfaces.Hr;
using ThinkOnErp.Infrastructure.Data;

namespace ThinkOnErp.Infrastructure.Repositories.Hr;

public sealed class LoanRepository : ILoanRepository
{
    private readonly OracleDbContext _context;

    public LoanRepository(OracleDbContext context)
    {
        _context = context;
    }

    public async Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeLoans
            .Include(l => l.Schedules)
            .AsQueryable();

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(l => l.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(status))
            query = query.Where(l => l.Status == status);

        return await query.OrderByDescending(l => l.StartDate).ToListAsync(cancellationToken);
    }

    public async Task<EmployeeLoan?> GetLoanByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeLoans
            .Include(l => l.Schedules)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task AddLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeLoans.AddAsync(loan, cancellationToken);
    }

    public Task UpdateLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default)
    {
        _context.EmployeeLoans.Update(loan);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<LoanRepaymentSchedule>> GetPendingSchedulesForPeriodAsync(string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRepaymentSchedules
            .Include(s => s.EmployeeLoan)
            .Where(s => s.PayPeriod == payPeriod && (s.Status == "PENDING" || s.Status == "PARTIAL") && s.EmployeeLoan!.Status == "ACTIVE")
            .ToListAsync(cancellationToken);
    }

    public Task UpdateSchedulesAsync(IEnumerable<LoanRepaymentSchedule> schedules, CancellationToken cancellationToken = default)
    {
        _context.LoanRepaymentSchedules.UpdateRange(schedules);
        return Task.CompletedTask;
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeAdvances.AsQueryable();

        if (!string.IsNullOrEmpty(employeeCode))
            query = query.Where(a => a.EmployeeCode == employeeCode);

        if (!string.IsNullOrEmpty(targetPayPeriod))
            query = query.Where(a => a.TargetPayPeriod == targetPayPeriod);

        return await query.OrderByDescending(a => a.CreationDate).ToListAsync(cancellationToken);
    }

    public async Task<EmployeeAdvance?> GetAdvanceByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeAdvances.FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeAdvances.AddAsync(advance, cancellationToken);
    }

    public Task UpdateAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default)
    {
        _context.EmployeeAdvances.Update(advance);
        return Task.CompletedTask;
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
