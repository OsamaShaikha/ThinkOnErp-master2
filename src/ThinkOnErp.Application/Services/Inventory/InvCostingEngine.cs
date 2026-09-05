using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvCostingEngine : IInvCostingEngine
{
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IInvStockLedgerRepository _stockLedgerRepository;
    private readonly IInvCostLayerRepository _costLayerRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvLotSerialRepository _lotSerialRepository;
    private readonly ILogger<InvCostingEngine> _logger;

    public InvCostingEngine(
        IInvStockBalanceRepository stockBalanceRepository,
        IInvStockLedgerRepository stockLedgerRepository,
        IInvCostLayerRepository costLayerRepository,
        IInvItemRepository itemRepository,
        IInvLotSerialRepository lotSerialRepository,
        ILogger<InvCostingEngine> logger)
    {
        _stockBalanceRepository = stockBalanceRepository;
        _stockLedgerRepository = stockLedgerRepository;
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

    public async Task<ApiResponse<RecalculateCostResultDto>> RecalculateCostBatchAsync(
        RecalculateCostRequestDto request,
        string username,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting batch cost recalculation. ItemId: {ItemId}, WarehouseId: {WarehouseId}, FromDate: {FromDate}",
            request.ItemId, request.WarehouseId, request.FromDate);

        var result = new RecalculateCostResultDto();
        List<long> itemIdsToProcess = new();

        if (request.ItemId.HasValue && request.ItemId.Value > 0)
        {
            itemIdsToProcess.Add(request.ItemId.Value);
        }
        else
        {
            var (allItems, _) = await _itemRepository.GetAllAsync(null, 1, 10000, cancellationToken);
            itemIdsToProcess.AddRange(allItems.Where(i => i.IsActive).Select(i => i.Id));
        }

        foreach (var itemId in itemIdsToProcess)
        {
            var item = await _itemRepository.GetByIdAsync(itemId, cancellationToken);
            if (item == null) continue;

            var movements = await _stockLedgerRepository.GetChronologicalMovementsAsync(
                itemId,
                request.WarehouseId,
                request.FromDate,
                cancellationToken);

            if (movements.Count == 0) continue;

            var balances = await _stockBalanceRepository.GetByItemAsync(itemId, cancellationToken);
            var balance = balances.FirstOrDefault(b => !request.WarehouseId.HasValue || b.WarehouseId == request.WarehouseId.Value)
                ?? await _stockBalanceRepository.GetOrCreateAsync(itemId, request.WarehouseId ?? 1, null, cancellationToken);

            decimal oldAvg = balance.AvgCost;
            decimal runningQty = 0;
            decimal runningAvg = item.StandardCost > 0 ? item.StandardCost : 0;
            int invoiceLinesUpdated = 0;

            foreach (var m in movements)
            {
                var isOut = m.Direction == TransactionDirection.Out;

                if (!isOut)
                {
                    // IN movement (Purchase, Receipt, Opening, Adjustment In)
                    var incomingQty = m.Quantity;
                    var incomingCost = m.UnitCost > 0 ? m.UnitCost : (runningAvg > 0 ? runningAvg : item.StandardCost);
                    m.UnitCost = incomingCost;

                    var totalQty = runningQty + incomingQty;
                    if (totalQty > 0)
                    {
                        runningAvg = ((runningQty * runningAvg) + (incomingQty * incomingCost)) / totalQty;
                        runningAvg = Math.Round(runningAvg, 4);
                    }
                    else
                    {
                        runningAvg = incomingCost;
                    }

                    runningQty += incomingQty;
                    m.RunningBalanceQty = runningQty;
                    m.RunningBalanceValue = runningQty * runningAvg;
                }
                else
                {
                    // OUT movement (Sales Issue, Transfer Out, Scrap, Adjustment Out)
                    m.UnitCost = runningAvg;
                    runningQty -= m.Quantity;
                    m.RunningBalanceQty = runningQty;
                    m.RunningBalanceValue = runningQty * runningAvg;

                    if (!string.IsNullOrWhiteSpace(m.SourceDocId))
                    {
                        invoiceLinesUpdated++;
                    }
                }
            }

            balance.AvgCost = runningAvg;
            balance.OnHandQty = runningQty;
            balance.UpdatedAt = DateTime.UtcNow;

            await _stockBalanceRepository.UpdateBalanceAsync(balance, cancellationToken);

            result.TotalItemsProcessed++;
            result.TotalMovementsRecalculated += movements.Count;
            result.TotalInvoiceLinesUpdated += invoiceLinesUpdated;

            result.Details.Add(new ItemCostRecalculationSummaryDto
            {
                ItemId = item.Id,
                ItemCode = item.ItemCode,
                ItemName = item.ItemNameLocal,
                OldAvgCost = oldAvg,
                NewAvgCost = runningAvg,
                MovementsCount = movements.Count,
                InvoiceLinesCount = invoiceLinesUpdated
            });
        }

        await _stockLedgerRepository.SaveChangesAsync(cancellationToken);
        await _stockBalanceRepository.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Batch cost recalculation completed. Processed {Items} items, {Moves} movements",
            result.TotalItemsProcessed, result.TotalMovementsRecalculated);

        return ApiResponse<RecalculateCostResultDto>.CreateSuccess(result, "Cost recalculation completed successfully");
    }
}
