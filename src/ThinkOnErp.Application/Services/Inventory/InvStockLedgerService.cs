using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;
using Microsoft.Extensions.Logging;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvStockLedgerService : IInvStockLedgerService
{
    private readonly IInvStockLedgerRepository _stockLedgerRepository;
    private readonly IInvStockBalanceRepository _stockBalanceRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvWarehouseRepository _warehouseRepository;
    private readonly IInvCostLayerRepository _costLayerRepository;
    private readonly IInvCostingEngine _costingEngine;
    private readonly ILogger<InvStockLedgerService> _logger;

    public InvStockLedgerService(
        IInvStockLedgerRepository stockLedgerRepository,
        IInvStockBalanceRepository stockBalanceRepository,
        IInvItemRepository itemRepository,
        IInvWarehouseRepository warehouseRepository,
        IInvCostLayerRepository costLayerRepository,
        IInvCostingEngine costingEngine,
        ILogger<InvStockLedgerService> logger)
    {
        _stockLedgerRepository = stockLedgerRepository;
        _stockBalanceRepository = stockBalanceRepository;
        _itemRepository = itemRepository;
        _warehouseRepository = warehouseRepository;
        _costLayerRepository = costLayerRepository;
        _costingEngine = costingEngine;
        _logger = logger;
    }

    public async Task<ApiResponse<StockMovementDto>> PostMovementAsync(StockMovementRequestDto request, CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Posting stock movement for item {ItemId} in warehouse {WarehouseId}, qty {Qty}",
            request.ItemId, request.WarehouseId, request.Quantity);

        if (request.Quantity <= 0)
        {
            return ApiResponse<StockMovementDto>.CreateFailure("Movement quantity must be greater than zero", null, 400);
        }

        var item = await _itemRepository.GetByIdAsync(request.ItemId, cancellationToken);
        if (item == null || !item.IsActive)
        {
            return ApiResponse<StockMovementDto>.CreateFailure("Item not found or inactive", null, 404);
        }

        var warehouse = await _warehouseRepository.GetByIdAsync(request.WarehouseId, cancellationToken);
        if (warehouse == null || !warehouse.IsActive)
        {
            return ApiResponse<StockMovementDto>.CreateFailure("Warehouse not found or inactive", null, 404);
        }

        if (item.LotTracking && string.IsNullOrWhiteSpace(request.LotNumber))
        {
            return ApiResponse<StockMovementDto>.CreateFailure("Lot number is required for this item", null, 400);
        }

        if (item.SerialTracking && string.IsNullOrWhiteSpace(request.SerialNumber))
        {
            return ApiResponse<StockMovementDto>.CreateFailure("Serial number is required for this item", null, 400);
        }

        var isOut = request.TransactionType < 0
            || (request.TransactionType >= 1000 && request.TransactionType < 1500)
            || (request.TransactionType >= 2500 && request.TransactionType < 3000)
            || (request.TransactionType >= 3010 && request.TransactionType < 3020);
        var balance = await _stockBalanceRepository.GetOrCreateAsync(request.ItemId, request.WarehouseId, request.BinId, cancellationToken);

        decimal unitCost = request.UnitCost;

        if (isOut)
        {
            if (balance.OnHandQty < request.Quantity && !item.AllowNegativeStock)
            {
                return ApiResponse<StockMovementDto>.CreateFailure(
                    $"Insufficient stock. Current on-hand: {balance.OnHandQty}, requested: {request.Quantity}", null, 400);
            }

            if (unitCost <= 0)
            {
                var costResp = await _costingEngine.GetCostForIssueAsync(item.Id, request.Quantity, item.CostingMethod.ToString(), cancellationToken);
                unitCost = costResp.Data;
            }

            balance.OnHandQty -= request.Quantity;
            balance.LastIssueDate = DateTime.UtcNow;
        }
        else
        {
            if (unitCost <= 0) unitCost = item.StandardCost;

            var avgResp = await _costingEngine.RecalculateWeightedAverageAsync(item.Id, request.Quantity, unitCost, cancellationToken);
            balance.AvgCost = avgResp.Data;
            balance.OnHandQty += request.Quantity;
            balance.LastReceiptDate = DateTime.UtcNow;

            if (item.CostingMethod == CostingMethod.Fifo)
            {
                await _costLayerRepository.AddLayerAsync(new InvCostLayer
                {
                    ItemId = item.Id,
                    WarehouseId = warehouse.Id,
                    ReceivedQty = request.Quantity,
                    RemainingQty = request.Quantity,
                    UnitCost = unitCost,
                    ReceivedDate = DateTime.UtcNow,
                    CreationDate = DateTime.UtcNow
                }, cancellationToken);
            }
        }

        balance.UpdatedAt = DateTime.UtcNow;

        var entry = new InvStockLedgerEntry
        {
            BranchId = warehouse.BranchId,
            ItemId = request.ItemId,
            WarehouseId = request.WarehouseId,
            BinId = request.BinId,
            TransactionDate = DateTime.UtcNow,
            TransactionType = (TransactionType)Math.Abs(request.TransactionType),
            Direction = isOut ? TransactionDirection.Out : TransactionDirection.In,
            Quantity = request.Quantity,
            UomCode = request.UomCode > 0 ? request.UomCode : item.UomBase,
            UnitCost = unitCost,
            RunningBalanceQty = balance.OnHandQty,
            RunningBalanceValue = balance.OnHandQty * balance.AvgCost,
            LpnCode = request.LpnCode,
            SourceModule = request.SourceModule ?? "INVENTORY",
            SourceDocType = request.SourceDocType,
            SourceDocId = request.SourceDocId,
            Notes = request.Notes,
            CreationUser = "admin",
            CreationDate = DateTime.UtcNow
        };

        await _stockLedgerRepository.AddEntryAsync(entry, cancellationToken);
        await _stockBalanceRepository.UpdateBalanceAsync(balance, cancellationToken);
        await _stockLedgerRepository.SaveChangesAsync(cancellationToken);
        await _stockBalanceRepository.SaveChangesAsync(cancellationToken);

        entry.Item = item;
        entry.Warehouse = warehouse;

        return ApiResponse<StockMovementDto>.CreateSuccess(InvStockMapper.ToDto(entry), "Stock movement posted successfully", 201);
    }

    public async Task<ApiResponse<List<StockMovementDto>>> GetMovementsByItemAsync(long itemId, CancellationToken cancellationToken = default)
    {
        var (entries, _) = await _stockLedgerRepository.GetByItemAsync(itemId, 1, 100, cancellationToken);
        var dtos = entries.Select(InvStockMapper.ToDto).ToList();
        return ApiResponse<List<StockMovementDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<List<StockMovementDto>>> GetMovementsByWarehouseAsync(long warehouseId, CancellationToken cancellationToken = default)
    {
        var (entries, _) = await _stockLedgerRepository.GetByWarehouseAsync(warehouseId, 1, 100, cancellationToken);
        var dtos = entries.Select(InvStockMapper.ToDto).ToList();
        return ApiResponse<List<StockMovementDto>>.CreateSuccess(dtos);
    }

    public async Task<ApiResponse<List<StockMovementDto>>> GetMovementsByDateRangeAsync(DateTime fromDate, DateTime toDate, CancellationToken cancellationToken = default)
    {
        var (entries, _) = await _stockLedgerRepository.GetByDateRangeAsync(fromDate, toDate, 1, 100, cancellationToken);
        var dtos = entries.Select(InvStockMapper.ToDto).ToList();
        return ApiResponse<List<StockMovementDto>>.CreateSuccess(dtos);
    }
}
