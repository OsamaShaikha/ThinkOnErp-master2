using ThinkOnErp.Application.DTOs.Hr;
using ThinkOnErp.Application.Services.Hr;

namespace ThinkOnErp.Application.Features.Hr.Employees.Common;

// These two adapters exist only so the obsolete EmployeeService facade can keep its old
// "unitOfWork may be null" constructor contract while the handlers take a required IHrUnitOfWork.
// Delete them together with EmployeeService once tests and HrSchemaTool call the handlers directly.

/// <summary>Used when no unit of work was supplied: every protected write is refused.</summary>
internal sealed class UnavailableHrUnitOfWork : IHrUnitOfWork
{
    private static InvalidOperationException Missing() =>
        new("Payroll-protected employee changes require a configured unit of work.");

    public Task<T> ExecuteEmployeeAccountAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<EmployeeImportResultDto> ExecuteEmployeeImportAsync(IReadOnlyList<EmployeeImportRowDto> rows, Func<Task<EmployeeImportResultDto>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteNewEmployeeAsync<T>(EmployeeImportRowDto row, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteEmployeeProfileAsync<T>(string employeeCode, DateTime earliestDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteForPayrollPeriodAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteForPayrollPeriodsAsync<T>(long companyId, string requiredPeriodCode, IReadOnlyCollection<string> affectedPeriodCodes, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecutePayrollFinancialAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteForEmployeeDatesAsync<T>(string employeeCode, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
    public Task<T> ExecuteForCompanyPayrollDatesAsync<T>(long companyId, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => throw Missing();
}

/// <summary>
/// Trusted internal callers without a unit of work (no user, no field checks) historically ran
/// salary assignment without a lock. This preserves that behaviour for the facade only.
/// </summary>
internal sealed class PassThroughHrUnitOfWork : IHrUnitOfWork
{
    public Task<T> ExecuteEmployeeAccountAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<EmployeeImportResultDto> ExecuteEmployeeImportAsync(IReadOnlyList<EmployeeImportRowDto> rows, Func<Task<EmployeeImportResultDto>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteNewEmployeeAsync<T>(EmployeeImportRowDto row, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteEmployeeProfileAsync<T>(string employeeCode, DateTime earliestDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteForPayrollPeriodAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteForPayrollPeriodsAsync<T>(long companyId, string requiredPeriodCode, IReadOnlyCollection<string> affectedPeriodCodes, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecutePayrollFinancialAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteForEmployeeDatesAsync<T>(string employeeCode, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
    public Task<T> ExecuteForCompanyPayrollDatesAsync<T>(long companyId, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default) => action();
}
