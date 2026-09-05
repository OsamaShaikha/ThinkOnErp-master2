using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEmployeeDependentRepository
{
    Task<IReadOnlyList<EmployeeDependent>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmployeeDependent?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddAsync(EmployeeDependent dependent, CancellationToken cancellationToken = default);
    void Update(EmployeeDependent dependent);
    void Remove(EmployeeDependent dependent);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
