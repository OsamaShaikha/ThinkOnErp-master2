using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IDepartmentRepository
{
    Task<IReadOnlyList<Department>> GetAllAsync(long? branchId = null, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<Department?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasChildrenAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasEmployeesAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Department department, CancellationToken cancellationToken = default);
    void Update(Department department);
    void Remove(Department department);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
