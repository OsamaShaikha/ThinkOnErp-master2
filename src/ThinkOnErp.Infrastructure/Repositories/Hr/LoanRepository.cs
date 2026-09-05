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

public sealed class LoanRepository : ILoanRepository
{
    private readonly OracleDbContext _context;

    public LoanRepository(OracleDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeLoans
            .Include(l => l.Employee)
            .Include(l => l.Schedules)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(l => l.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(l => l.Status == status);
        }

        return await query.OrderByDescending(l => l.StartDate).ToListAsync(cancellationToken);
    }

    public async Task<EmployeeLoan?> GetLoanByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeLoans
            .Include(l => l.Employee)
            .Include(l => l.Schedules)
            .FirstOrDefaultAsync(l => l.Id == id, cancellationToken);
    }

    public async Task AddLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeLoans.AddAsync(loan, cancellationToken);
    }

    public void UpdateLoan(EmployeeLoan loan)
    {
        _context.EmployeeLoans.Update(loan);
    }

    public async Task<IReadOnlyList<LoanRepaymentSchedule>> GetSchedulesByLoanIdAsync(long loanId, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRepaymentSchedules
            .AsNoTracking()
            .Where(s => s.EmployeeLoanId == loanId)
            .OrderBy(s => s.InstallmentNo)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<LoanRepaymentSchedule>> GetPendingSchedulesByPeriodAsync(string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRepaymentSchedules
            .Include(s => s.EmployeeLoan)
            .ThenInclude(l => l!.Employee)
            .Where(s => s.PayPeriod == payPeriod && (s.Status == "PENDING" || s.Status == "PARTIAL"))
            .Where(s => s.EmployeeLoan != null && (s.EmployeeLoan.Status == "ACTIVE" || s.EmployeeLoan.Status == "APPROVED"))
            .ToListAsync(cancellationToken);
    }

    public async Task<LoanRepaymentSchedule?> GetScheduleAsync(long loanId, string payPeriod, CancellationToken cancellationToken = default)
    {
        return await _context.LoanRepaymentSchedules
            .FirstOrDefaultAsync(s => s.EmployeeLoanId == loanId && s.PayPeriod == payPeriod, cancellationToken);
    }

    public async Task AddScheduleAsync(LoanRepaymentSchedule schedule, CancellationToken cancellationToken = default)
    {
        await _context.LoanRepaymentSchedules.AddAsync(schedule, cancellationToken);
    }

    public void UpdateSchedule(LoanRepaymentSchedule schedule)
    {
        _context.LoanRepaymentSchedules.Update(schedule);
    }

    public async Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode = null, string? payPeriod = null, string? status = null, CancellationToken cancellationToken = default)
    {
        var query = _context.EmployeeAdvances
            .Include(a => a.Employee)
            .AsNoTracking();

        if (!string.IsNullOrWhiteSpace(employeeCode))
        {
            query = query.Where(a => a.EmployeeCode == employeeCode);
        }

        if (!string.IsNullOrWhiteSpace(payPeriod))
        {
            query = query.Where(a => a.TargetPayPeriod == payPeriod);
        }

        if (!string.IsNullOrWhiteSpace(status))
        {
            query = query.Where(a => a.Status == status);
        }

        return await query.OrderByDescending(a => a.CreationDate).ToListAsync(cancellationToken);
    }

    public async Task<EmployeeAdvance?> GetAdvanceByIdAsync(long id, CancellationToken cancellationToken = default)
    {
        return await _context.EmployeeAdvances
            .Include(a => a.Employee)
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken);
    }

    public async Task AddAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default)
    {
        await _context.EmployeeAdvances.AddAsync(advance, cancellationToken);
    }

    public void UpdateAdvance(EmployeeAdvance advance)
    {
        _context.EmployeeAdvances.Update(advance);
    }

    public async Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        await _context.SaveChangesAsync(cancellationToken);
    }
}
