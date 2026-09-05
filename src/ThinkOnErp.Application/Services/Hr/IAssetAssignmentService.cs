using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using ThinkOnErp.Application.DTOs.Hr;

namespace ThinkOnErp.Application.Services.Hr;

public interface IAssetAssignmentService
{
    Task<List<AssetAssignmentDto>> GetAllAssignmentsAsync(string? employeeCode = null, string? status = null, string? category = null);
    Task<AssetAssignmentDto?> GetAssignmentByIdAsync(long id);
    Task<AssetAssignmentDto> AssignAssetAsync(CreateAssetAssignmentDto dto, string currentUser);
    Task<AssetAssignmentDto> ReturnAssetAsync(long id, ReturnAssetDto dto, string currentUser);
}
