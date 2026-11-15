namespace ThinkOnErp.Application.Services.Hr;

public interface IHrUnitOfWork
{
    Task<T> ExecuteEmployeeAccountAsync<T>(string employeeCode, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<ThinkOnErp.Application.DTOs.Hr.EmployeeImportResultDto> ExecuteEmployeeImportAsync(
        IReadOnlyList<ThinkOnErp.Application.DTOs.Hr.EmployeeImportRowDto> rows,
        Func<Task<ThinkOnErp.Application.DTOs.Hr.EmployeeImportResultDto>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteNewEmployeeAsync<T>(ThinkOnErp.Application.DTOs.Hr.EmployeeImportRowDto row, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteEmployeeProfileAsync<T>(string employeeCode, DateTime earliestDate, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteAsync<T>(Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteForPayrollPeriodAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteForPayrollPeriodsAsync<T>(long companyId, string requiredPeriodCode, IReadOnlyCollection<string> affectedPeriodCodes,
        Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecutePayrollFinancialAsync<T>(long companyId, string periodCode, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteForEmployeeDatesAsync<T>(string employeeCode, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default);
    Task<T> ExecuteForCompanyPayrollDatesAsync<T>(long companyId, DateTime startDate, DateTime endDate, Func<Task<T>> action, CancellationToken cancellationToken = default);
}
