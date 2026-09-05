using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IDepartmentService
{
    Task<List<DepartmentDto>> GetAllAsync(long? branchId = null, bool activeOnly = true);
    Task<List<DepartmentTreeNodeDto>> GetTreeAsync(long? branchId = null);
    Task<DepartmentDto?> GetByCodeAsync(string code);
    Task<DepartmentDto> CreateAsync(CreateDepartmentDto dto, string currentUser);
    Task<DepartmentDto> UpdateAsync(string code, UpdateDepartmentDto dto, string currentUser);
    Task<bool> DeleteAsync(string code);
}
