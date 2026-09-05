using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IEmployeeService
{
    Task<List<EmployeeDto>> GetAllAsync(
        string? departmentCode = null,
        long? branchId = null,
        string? status = null,
        bool activeOnly = true);

    Task<EmployeeDto?> GetByCodeAsync(string employeeCode, bool includeDetails = true);
    Task<EmployeeDto> CreateAsync(CreateEmployeeDto dto, string currentUser);
    Task<EmployeeDto> UpdateAsync(string employeeCode, UpdateEmployeeDto dto, string currentUser);
    Task<EmployeeDto> ChangeStatusAsync(string employeeCode, ChangeEmployeeStatusDto dto, string currentUser);
    Task<EmploymentEventDto> RecordEventAsync(string employeeCode, RecordEmploymentEventDto dto, string currentUser);
    Task<List<EmploymentEventDto>> GetEventsAsync(string employeeCode);
    
    // Dependents management
    Task<List<EmployeeDependentDto>> GetDependentsAsync(string employeeCode);
    Task<EmployeeDependentDto> AddDependentAsync(string employeeCode, CreateEmployeeDependentDto dto, string currentUser);
    Task<EmployeeDependentDto> UpdateDependentAsync(long dependentId, UpdateEmployeeDependentDto dto, string currentUser);
    Task<bool> DeleteDependentAsync(long dependentId);
}
