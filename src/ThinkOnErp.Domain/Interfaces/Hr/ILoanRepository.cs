using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ILoanRepository
{
    // Loans
    Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode = null, string? status = null, CancellationToken cancellationToken = default);
    Task<EmployeeLoan?> GetLoanByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default);
    void UpdateLoan(EmployeeLoan loan);

    // Schedules
    Task<IReadOnlyList<LoanRepaymentSchedule>> GetSchedulesByLoanIdAsync(long loanId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<LoanRepaymentSchedule>> GetPendingSchedulesByPeriodAsync(string payPeriod, CancellationToken cancellationToken = default);
    Task<LoanRepaymentSchedule?> GetScheduleAsync(long loanId, string payPeriod, CancellationToken cancellationToken = default);
    Task AddScheduleAsync(LoanRepaymentSchedule schedule, CancellationToken cancellationToken = default);
    void UpdateSchedule(LoanRepaymentSchedule schedule);

    // Advances
    Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode = null, string? payPeriod = null, string? status = null, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance?> GetAdvanceByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default);
    void UpdateAdvance(EmployeeAdvance advance);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
