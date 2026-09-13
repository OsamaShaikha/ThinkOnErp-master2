using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosOrderService
{
    Task<ApiResponse<PosOrderSummaryDto>> CreateOrderAsync(CreatePosOrderDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> ProcessPaymentAsync(long orderId, List<CreatePosOrderPaymentDto> payments, string username, CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> ParkOrderAsync(long orderId, string username, CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> RefundOrderAsync(RefundOrderDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> VoidOrderLineAsync(VoidLineDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> GetOrderByIdAsync(long orderId, CancellationToken ct = default);
    Task<ApiResponse<IReadOnlyList<PosOrderSummaryDto>>> GetActiveOrdersAsync(long branchId, PosOrderStatus? status = null, CancellationToken ct = default);
    Task<ApiResponse<PagedResultDto<PosOrderSummaryDto>>> GetOrdersPagedAsync(
        long branchId,
        long? shiftId = null,
        PosOrderStatus? status = null,
        PosOrderType? orderType = null,
        DateTime? fromDate = null,
        DateTime? toDate = null,
        int pageIndex = 1,
        int pageSize = 20,
        CancellationToken ct = default);
    Task<ApiResponse<PosOrderSummaryDto>> UpdateOrderAsync(long orderId, UpdatePosOrderDto dto, string username, CancellationToken ct = default);
    Task<ApiResponse<bool>> DeleteOrderAsync(long orderId, string username, CancellationToken ct = default);
}
