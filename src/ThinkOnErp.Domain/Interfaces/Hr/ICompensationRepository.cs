using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Domain.Entities.Hr;

namespace ThinkOnErp.Domain.Interfaces.Hr;

public interface ICompensationRepository
{
    // Salary Components
    Task<IReadOnlyList<SalaryComponent>> GetAllComponentsAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<SalaryComponent?> GetComponentByCodeAsync(string code, CancellationToken cancellationToken = default);
    Task<bool> ComponentCodeExistsAsync(string code, CancellationToken cancellationToken = default);
    Task AddComponentAsync(SalaryComponent component, CancellationToken cancellationToken = default);
    void UpdateComponent(SalaryComponent component);

    // Salary Structure
    Task<EmployeeSalaryStructure?> GetActiveStructureAsync(string employeeCode, DateTime effectiveDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<EmployeeSalaryStructure>> GetStructureHistoryAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task AddStructureAsync(EmployeeSalaryStructure structure, CancellationToken cancellationToken = default);
    void UpdateStructure(EmployeeSalaryStructure structure);

    // Salary Revisions
    Task<IReadOnlyList<SalaryRevision>> GetRevisionsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task AddRevisionAsync(SalaryRevision revision, CancellationToken cancellationToken = default);

    // Contracts
    Task<IReadOnlyList<EmploymentContract>> GetContractsByEmployeeAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmploymentContract?> GetActiveContractAsync(string employeeCode, CancellationToken cancellationToken = default);
    Task<EmploymentContract?> GetContractByIdAsync(long id, CancellationToken cancellationToken = default);
    Task AddContractAsync(EmploymentContract contract, CancellationToken cancellationToken = default);
    void UpdateContract(EmploymentContract contract);

    Task SaveChangesAsync(CancellationToken cancellationToken = default);
}
