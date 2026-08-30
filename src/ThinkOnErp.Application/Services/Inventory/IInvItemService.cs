using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Items;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvItemService
{
    Task<ApiResponse<InvItemDto>> CreateAsync(CreateInvItemDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvItemDto>> UpdateAsync(long id, UpdateInvItemDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvItemDto>> GetByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<InvItemListDto>>> GetAllAsync(int pageNumber, int pageSize, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeactivateAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> ImportFromExcelAsync(byte[] excelData, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AddUomConversionAsync(long itemId, CreateInvItemUomDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteUomConversionAsync(long itemId, long uomId, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> AddBarcodeAsync(long itemId, CreateInvItemBarcodeDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteBarcodeAsync(long itemId, long barcodeId, CancellationToken cancellationToken = default);
}
