using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEmployeeRepository
{
    Task<IReadOnlyList<Employee>> GetAllAsync(
        string? departmentCode = null,
        long? branchId = null,
        string? status = null,
        bool activeOnly = true,
        CancellationToken cancellationToken = default);

    Task<Employee?> GetByCodeAsync(string employeeCode, bool includeDetails = false, CancellationToken cancellationToken = default);
    Task<Employee?> GetByNationalIdAsync(string nationalId, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<bool> NationalIdExistsAsync(string nationalId, string? excludeEmployeeCode = null, CancellationToken cancellationToken = default);
    Task AddAsync(Employee employee, CancellationToken cancellationToken = default);
    void Update(Employee employee);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
