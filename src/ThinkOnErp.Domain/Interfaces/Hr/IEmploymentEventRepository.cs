using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEmploymentEventRepository
{
    Task<IReadOnlyList<EmploymentEvent>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task AddAsync(EmploymentEvent employmentEvent, CancellationToken cancellationToken = default);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
