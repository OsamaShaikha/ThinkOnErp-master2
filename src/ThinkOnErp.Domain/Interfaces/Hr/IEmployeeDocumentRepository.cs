using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface IEmployeeDocumentRepository
{
    Task<IReadOnlyList<EmployeeDocument>> GetByEmployeeCodeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmployeeDocument?> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeDocument>> GetExpiringDocumentsAsync(DateTime thresholdDate, CancellationToken cancellationToken = default);
    Task AddAsync(EmployeeDocument document, CancellationToken cancellationToken = default);
    void Update(EmployeeDocument document);
    void Remove(EmployeeDocument document);
    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
