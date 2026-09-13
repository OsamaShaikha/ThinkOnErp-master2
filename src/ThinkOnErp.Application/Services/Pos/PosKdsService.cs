using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Pos;
using ThinkOnErp.Domain.Entities.Pos.Enums;
using ThinkOnErp.Domain.Interfaces.Pos;

namespace ThinkOnErp.Application.Services.Pos;

public interface IPosKdsService
{
    Task<ApiResponse<IReadOnlyList<KdsTicketDto>>> GetActiveKitchenTicketsAsync(long branchId, string? station = null, CancellationToken ct = default);
    Task<ApiResponse<bool>> UpdateLineStatusAsync(UpdateKdsLineStatusDto dto, CancellationToken ct = default);
    Task<ApiResponse<bool>> BumpTicketAsync(long orderId, CancellationToken ct = default);
}

public class PosKdsService : IPosKdsService
{
    private readonly IPosOrderRepository _orderRepository;

    public PosKdsService(IPosOrderRepository orderRepository)
    {
        _orderRepository = orderRepository;
    }

    public async Task<ApiResponse<IReadOnlyList<KdsTicketDto>>> GetActiveKitchenTicketsAsync(long branchId, string? station = null, CancellationToken ct = default)
    {
        var activeOrders = await _orderRepository.GetActiveOrdersAsync(branchId, null, ct);

        var tickets = new List<KdsTicketDto>();
        foreach (var order in activeOrders.Where(o => !o.IsRefund))
        {
            var eligibleLines = order.Lines
                .Where(l => !l.IsVoided && l.KdsStatus != PosKdsStatus.Served)
                .Where(l => string.IsNullOrWhiteSpace(station) || string.Equals(l.PrepStation, station, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (eligibleLines.Any())
            {
                tickets.Add(new KdsTicketDto
                {
                    OrderId = order.Id,
                    OrderNumber = order.OrderNumber,
                    OrderType = order.OrderType,
                    TableNumber = order.Table?.TableNumber,
                    CustomerName = order.CustomerName,
                    CreatedAt = order.CreationDate,
                    Notes = order.Notes,
                    Lines = eligibleLines.Select(l => new KdsTicketLineDto
                    {
                        LineId = l.Id,
                        ItemName = l.ItemName,
                        Quantity = l.Quantity,
                        Status = l.KdsStatus,
                        PrepStation = l.PrepStation,
                        SpecialInstructions = l.SpecialInstructions,
                        Modifiers = l.Modifiers.Select(m => m.ModifierName).ToList()
                    }).ToList()
                });
            }
        }

        return ApiResponse<IReadOnlyList<KdsTicketDto>>.CreateSuccess(tickets.OrderBy(t => t.CreatedAt).ToList());
    }

    public async Task<ApiResponse<bool>> UpdateLineStatusAsync(UpdateKdsLineStatusDto dto, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(dto.OrderId, ct);
        if (order == null)
            return ApiResponse<bool>.CreateFailure("Order not found", null, 404);

        var line = order.Lines.FirstOrDefault(l => l.Id == dto.LineId);
        if (line == null)
            return ApiResponse<bool>.CreateFailure("Order line not found", null, 404);

        line.KdsStatus = dto.NewStatus;
        if (dto.NewStatus == PosKdsStatus.Ready)
            line.KdsReadyAt = DateTime.UtcNow;

        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "KDS line status updated");
    }

    public async Task<ApiResponse<bool>> BumpTicketAsync(long orderId, CancellationToken ct = default)
    {
        var order = await _orderRepository.GetOrderByIdAsync(orderId, ct);
        if (order == null)
            return ApiResponse<bool>.CreateFailure("Order not found", null, 404);

        foreach (var line in order.Lines.Where(l => !l.IsVoided))
        {
            line.KdsStatus = PosKdsStatus.Ready;
            line.KdsReadyAt = DateTime.UtcNow;
        }

        order.Status = PosOrderStatus.Ready;
        await _orderRepository.UpdateOrderAsync(order, ct);
        await _orderRepository.SaveChangesAsync(ct);

        return ApiResponse<bool>.CreateSuccess(true, "Ticket bumped to Ready status");
    }
}
