using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Settings;

namespace ThinkOnErp.Application.Services.Settings;

/// <summary>
/// Service interface for unified management of Company-level and Branch-level settings.
/// </summary>
public interface ISettingsService
{
    Task<ApiResponse<CompanySettingsDto>> GetCompanySettingsAsync(CancellationToken ct = default);
    Task<ApiResponse<CompanySettingsDto>> UpdateCompanySettingsAsync(UpdateCompanySettingsDto dto, string username, CancellationToken ct = default);

    Task<ApiResponse<List<BranchSettingsDto>>> GetAllBranchSettingsAsync(CancellationToken ct = default);
    Task<ApiResponse<BranchSettingsDto>> GetBranchSettingsAsync(long branchId, CancellationToken ct = default);
    Task<ApiResponse<BranchSettingsDto>> UpdateBranchSettingsAsync(long branchId, UpdateBranchSettingsDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> ResetBranchSettingsAsync(long branchId, CancellationToken ct = default);
}
