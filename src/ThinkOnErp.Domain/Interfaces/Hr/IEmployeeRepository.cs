using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEmployeeRepository
{
    Task<(IReadOnlyList<Employee> Items, int TotalCount)> GetEmployeesAsync(
        string? searchKeyword = null,
        string? departmentCode = null,
        string? status = null,
        long? branchId = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetEmployeeByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<bool> ExistsByNationalIdAsync(string nationalId, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default);
    Task AddEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task UpdateEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);
    Task DeleteEmployeeAsync(Employee employee, CancellationToken cancellationToken = default);

    Task<EmployeeDependent?> GetDependentByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddDependentAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default);
    Task DeleteDependentAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default);

    Task<SalaryStructure?> GetActiveSalaryStructureAsync(string employeeCode, DateTime asOfDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalaryStructure>> GetSalaryStructuresAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task AddSalaryStructureAsync(SalaryStructure structure, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalaryComponent>> GetSalaryComponentsAsync(CancellationToken cancellationToken = default);
    Task<SalaryComponent?> GetSalaryComponentByCodeAsync(string componentCode, CancellationToken cancellationToken = default);
    Task AddSalaryComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
