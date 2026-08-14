using ThinkOnErp.Application.DTOs.Accounting.CostCenters;

namespace ThinkOnErp.Application.Services.Accounting;

public interface IGlCostCenterService
{
    Task<IReadOnlyList<GlCostCenterDto>> GetAllAsync(CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GlCostCenterTreeDto>> GetTreeAsync(CancellationToken cancellationToken = default);
    Task<GlCostCenterDto?> GetByCodeAsync(string costCenterCode, CancellationToken cancellationToken = default);
    Task<GlCostCenterDto> CreateAsync(CreateGlCostCenterDto dto, string username = "SYSTEM", CancellationToken cancellationToken = default);
    Task<GlCostCenterDto> UpdateAsync(string costCenterCode, UpdateGlCostCenterDto dto, string username = "SYSTEM", CancellationToken cancellationToken = default);
    Task<GlCostCenterDto> SetActiveStatusAsync(string costCenterCode, bool isActive, string username = "SYSTEM", CancellationToken cancellationToken = default);
}
