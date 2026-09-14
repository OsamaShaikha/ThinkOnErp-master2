using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ILoanRepository
{
    Task<IReadOnlyList<EmployeeLoan>> GetLoansAsync(string? employeeCode, string? status, CancellationToken cancellationToken = default);
    Task<EmployeeLoan?> GetLoanByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default);
    Task UpdateLoanAsync(EmployeeLoan loan, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LoanRepaymentSchedule>> GetPendingSchedulesForPeriodAsync(string payPeriod, CancellationToken cancellationToken = default);
    Task UpdateSchedulesAsync(IEnumerable<LoanRepaymentSchedule> schedules, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EmployeeAdvance>> GetAdvancesAsync(string? employeeCode, string? targetPayPeriod, CancellationToken cancellationToken = default);
    Task<EmployeeAdvance?> GetAdvanceByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default);
    Task UpdateAdvanceAsync(EmployeeAdvance advance, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
