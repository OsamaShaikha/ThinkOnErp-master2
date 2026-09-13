using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Features.Pos.Orders.Queries.GetActiveOrders;

public record GetActiveOrdersQuery(long BranchId, PosOrderStatus? Status = null) : IRequest<ApiResponse<IReadOnlyList<PosOrderSummaryDto>>>;

public class GetActiveOrdersQueryHandler : IRequestHandler<GetActiveOrdersQuery, ApiResponse<IReadOnlyList<PosOrderSummaryDto>>>
{
    private readonly IPosOrderService _orderService;

    public GetActiveOrdersQueryHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<IReadOnlyList<PosOrderSummaryDto>>> Handle(GetActiveOrdersQuery request, CancellationToken cancellationToken)
    {
        return await _orderService.GetActiveOrdersAsync(request.BranchId, request.Status, cancellationToken);
    }
}
