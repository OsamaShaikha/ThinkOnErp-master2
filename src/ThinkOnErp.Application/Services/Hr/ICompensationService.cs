using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface ICompensationService
{
    // Salary Components
    Task<List<SalaryComponentDto>> GetAllComponentsAsync(bool activeOnly = true);
    Task<SalaryComponentDto?> GetComponentByCodeAsync(string code);
    Task<SalaryComponentDto> CreateComponentAsync(CreateSalaryComponentDto dto, string currentUser);

    // Salary Structure
    Task<EmployeeSalaryStructureDto?> GetActiveStructureAsync(string employeeCode, DateTime? effectiveDate = null);
    Task<List<EmployeeSalaryStructureDto>> GetStructureHistoryAsync(string employeeCode);
    Task<EmployeeSalaryStructureDto> SetStructureAsync(string employeeCode, SetEmployeeSalaryStructureDto dto, string currentUser);

    // Salary Revisions
    Task<List<SalaryRevisionDto>> GetSalaryRevisionsAsync(string employeeCode);
    Task<SalaryRevisionDto> CreateRevisionAsync(string employeeCode, CreateSalaryRevisionDto dto, string currentUser);

    // Contracts
    Task<List<EmploymentContractDto>> GetContractsAsync(string employeeCode);
    Task<EmploymentContractDto?> GetActiveContractAsync(string employeeCode);
    Task<EmploymentContractDto> CreateContractAsync(string employeeCode, CreateEmploymentContractDto dto, string currentUser);
}
