using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.PriceLists;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvPriceListService
{
    Task<ApiResponse<IReadOnlyList<PriceListDto>>> GetPriceListsByBranchAsync(long branchId, PosOrderType? orderType = null, bool? isActive = null, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> GetPriceListByIdAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> CreatePriceListAsync(CreatePriceListDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PriceListDetailsDto>> UpdatePriceListAsync(long id, UpdatePriceListDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeletePriceListAsync(long id, CancellationToken ct = default);
    Task<ApiResponse<PriceListItemDto>> UpsertItemAsync(long priceListId, UpsertPriceListItemDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> RemoveItemAsync(long priceListId, long itemId, CancellationToken ct = default);
}
