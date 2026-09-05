using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IJobGradeRepository
{
    Task<IReadOnlyList<JobGrade>> GetAllAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<JobGrade?> GetByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> CodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> HasPositionsAsync(string code, CancellationToken cancellationToken = default);
    Task AddAsync(JobGrade jobGrade, CancellationToken cancellationToken = default);
    void Update(JobGrade jobGrade);
    void Remove(JobGrade jobGrade);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
