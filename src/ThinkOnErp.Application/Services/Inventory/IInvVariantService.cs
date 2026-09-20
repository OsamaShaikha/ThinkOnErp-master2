using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Variants;

namespace ThinkOnErp.Application.Services.Inventory;

public interface IInvVariantService
{
    // Attributes
    Task<ApiResponse<List<InvAttributeDto>>> GetAttributesAsync(bool activeOnly = true, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvAttributeDto>> CreateAttributeAsync(CreateAttributeDto request, CancellationToken cancellationToken = default);

    // Variants
    Task<ApiResponse<PagedResultDto<InvVariantDto>>> GetVariantsPagedAsync(
        int pageNumber, int pageSize, long? itemId = null, string? search = null, bool? activeOnly = null, CancellationToken cancellationToken = default);

    Task<ApiResponse<List<InvVariantDto>>> GetVariantsByItemIdAsync(long itemId, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvVariantDto>> GetVariantByIdAsync(long id, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvVariantDto>> CreateVariantManualAsync(CreateVariantManualDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<List<InvVariantDto>>> GenerateVariantsMatrixAsync(GenerateVariantsMatrixRequestDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<InvVariantDto>> UpdateVariantAsync(long id, UpdateVariantDto request, CancellationToken cancellationToken = default);
    Task<ApiResponse<bool>> DeleteVariantAsync(long id, CancellationToken cancellationToken = default);
}
