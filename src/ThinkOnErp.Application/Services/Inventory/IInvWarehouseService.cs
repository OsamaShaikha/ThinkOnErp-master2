using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Warehouses;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvWarehouseService
{
    Task<ApiResponse<InvWarehouseDto>> CreateAsync(CreateInvWarehouseDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvWarehouseDto>> UpdateAsync(long id, UpdateInvWarehouseDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvWarehouseDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<InvWarehouseDto>>> GetAllByBranchAsync(long branchId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteWarehouseAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AddZoneAsync(long warehouseId, CreateInvZoneDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateZoneAsync(long zoneId, UpdateInvZoneDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteZoneAsync(long zoneId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AddBinAsync(long zoneId, CreateInvBinDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> UpdateBinAsync(long binId, UpdateInvBinDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteBinAsync(long binId, CancellationToken cancellationToken = default);
    Task<ApiResponse<object>> GetStockSummaryAsync(long warehouseId, CancellationToken cancellationToken = default);
}
