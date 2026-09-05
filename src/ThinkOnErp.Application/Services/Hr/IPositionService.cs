using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IPositionService
{
    Task<List<PositionDto>> GetAllAsync(string? departmentCode = null, bool activeOnly = true);
    Task<PositionDto?> GetByCodeAsync(string code);
    Task<List<OrgChartNodeDto>> GetOrgChartAsync(long? branchId = null);
    Task<PositionDto> CreateAsync(CreatePositionDto dto, string currentUser);
    Task<PositionDto> UpdateAsync(string code, UpdatePositionDto dto, string currentUser);
    Task<bool> DeleteAsync(string code);
}
