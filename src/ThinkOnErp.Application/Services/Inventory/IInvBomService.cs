using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Bom;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvBomService
{
    Task<ApiResponse<InvBomDto>> CreateBomAsync(CreateInvBomDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<InvBomDto>> GetByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<InvBomDto>> GetDefaultByParentItemIdAsync(long parentItemId, CancellationToken ct = default);
    Task<ApiResponse<List<InvBomDto>>> GetAllByParentItemIdAsync(long parentItemId, CancellationToken ct = default);
    Task<ApiResponse<(List<InvBomDto> Items, int TotalCount)>> GetPagedAsync(long? branchId, int pageIndex, int pageSize, CancellationToken ct = default);
    Task<ApiResponse<InvBomDto>> UpdateBomAsync(long id, UpdateInvBomDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteBomAsync(long id, CancellationToken ct = default);
}
