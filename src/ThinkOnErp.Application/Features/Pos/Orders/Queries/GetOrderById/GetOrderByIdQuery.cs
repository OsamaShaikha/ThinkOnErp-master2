using System.Collections.Generic;
using MediatR;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Application.Services.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Application.Features.Pos.Orders.Queries.GetOrderById;

public record GetOrderByIdQuery(long OrderId) : IRequest<ApiResponse<PosOrderSummaryDto>>;

public class GetOrderByIdQueryHandler : IRequestHandler<GetOrderByIdQuery, ApiResponse<PosOrderSummaryDto>>
{
    private readonly IPosOrderService _orderService;

    public GetOrderByIdQueryHandler(IPosOrderService orderService)
    {
        _orderService = orderService;
    }

    public async Task<ApiResponse<PosOrderSummaryDto>> Handle(GetOrderByIdQuery request, CancellationToken cancellationToken)
    {
        return await _orderService.GetOrderByIdAsync(request.OrderId, cancellationToken);
    }
}
