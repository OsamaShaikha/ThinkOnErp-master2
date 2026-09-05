using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IPositionRepository
{
    Task<IReadOnlyList<Position>> GetAllAsync(string? departmentCode = null, bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<Position?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasReportsAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasEmployeesAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(Position position, CancellationToken cancellationToken = default);
    void Update(Position position);
    void Remove(Position position);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
