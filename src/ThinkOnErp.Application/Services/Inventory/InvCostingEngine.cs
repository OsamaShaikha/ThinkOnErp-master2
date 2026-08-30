using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvCostingEngine : IInvCostingEngine
{
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IInvCostLayerRepository _costLayerRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvLotSerialRepository _lotSerialRepository;
    private readonly ILogger<InvCostingEngine> _logger;

    public InvCostingEngine(
        IInvStockBalanceRepository stockBalanceRepository,
        IInvCostLayerRepository costLayerRepository,
        IInvItemRepository itemRepository,
        IInvLotSerialRepository lotSerialRepository,
        ILogger<InvCostingEngine> logger)
    {
        _stockBalanceRepository = stockBalanceRepository;
        _costLayerRepository = costLayerRepository;
        _itemRepository = itemRepository;
        _lotSerialRepository = lotSerialRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<decimal>> RecalculateWeightedAverageAsync(long itemId, decimal incomingQty, decimal incomingCost, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Recalculating weighted average for item {ItemId}, incoming qty: {Qty}, cost: {Cost}", itemId, incomingQty, incomingCost);

        var balances = await _stockBalanceRepository.GetByItemAsync(itemId, cancellationToken);
        var currentOnHand = balances.Sum(b => b.OnHandQty);
        var currentAvg = balances.FirstOrDefault()?.AvgCost ?? 0m;

        var totalQty = currentOnHand + incomingQty;
        if (totalQty <= 0)
        {
            return ApiResponse<decimal>.CreateSuccess(incomingCost);
        }

        var newAvg = ((currentOnHand * currentAvg) + (incomingQty * incomingCost)) / totalQty;
        newAvg = Math.Round(newAvg, 4);

        return ApiResponse<decimal>.CreateSuccess(newAvg);
    }

    public async Task<ApiResponse<bool>> ConsumeFifoLayersAsync(long itemId, decimal quantityToConsume, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Consuming FIFO layers for item {ItemId}, qty {Qty}", itemId, quantityToConsume);

        var layers = await _costLayerRepository.GetAvailableLayersAsync(itemId, 0, cancellationToken);
        var remainingNeeded = quantityToConsume;

        foreach (var layer in layers)
        {
            if (remainingNeeded <= 0) break;

            var take = Math.Min(layer.RemainingQty, remainingNeeded);
            layer.RemainingQty -= take;
            remainingNeeded -= take;

            await _costLayerRepository.UpdateRemainingQtyAsync(layer.Id, layer.RemainingQty, cancellationToken);
        }

        await _costLayerRepository.SaveChangesAsync(cancellationToken);
        return ApiResponse<bool>.CreateSuccess(true);
    }

    public async Task<ApiResponse<decimal>> CalculateStandardCostVarianceAsync(long itemId, decimal actualCost, CancellationToken cancellationToken = default)
    {
        var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
        if (item == null)
        {
            return ApiResponse<decimal>.CreateFailure("Item not found", null, 404);
        }

        var ppv = actualCost - item.StandardCost;
        return ApiResponse<decimal>.CreateSuccess(ppv);
    }

    public async Task<ApiResponse<object>> GetFefoLotAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var lots = await _lotSerialRepository.GetAvailableLotsFefoAsync(itemId, cancellationToken);
        var activeLot = lots.FirstOrDefault();
        return ApiResponse<object>.CreateSuccess(activeLot ?? new object());
    }

    public async Task<ApiResponse<decimal>> GetCostForIssueAsync(long itemId, decimal quantity, string costingMethod, CancellationToken cancellationToken = default)
    {
        var balances = await _stockBalanceRepository.GetByItemAsync(itemId, cancellationToken);
        var currentAvg = balances.FirstOrDefault()?.AvgCost ?? 0m;

        if (costingMethod.Equals("Standard", StringComparison.OrdinalIgnoreCase))
        {
            var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
            return ApiResponse<decimal>.CreateSuccess(item?.StandardCost ?? currentAvg);
        }

        return ApiResponse<decimal>.CreateSuccess(currentAvg);
    }
}
