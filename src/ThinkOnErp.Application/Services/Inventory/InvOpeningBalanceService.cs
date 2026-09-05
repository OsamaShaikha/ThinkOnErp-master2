using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InvOpeningBalanceService : IInvOpeningBalanceService
{
    private readonly IInvOpeningBalanceRepository _repo;
    private readonly IInvStockLedgerService _stockLedgerService;
    private readonly ILogger<InvOpeningBalanceService> _logger;

    public InvOpeningBalanceService(
        IInvOpeningBalanceRepository repo,
        IInvStockLedgerService stockLedgerService,
        ILogger<InvOpeningBalanceService> logger)
    {
        _repo = repo;
        _stockLedgerService = stockLedgerService;
        _logger = logger;
    }

    public async Task<ApiResponse<OpeningBatchDto>> CreateBatchAsync(CreateOpeningBatchDto dto, string username, CancellationToken ct = default)
    {
        var batchNo = $"OPB-{DateTime.UtcNow.Year}-{DateTime.UtcNow.Ticks % 1000000:D6}";
        var batch = new InvOpeningBatch
        {
            BranchId = dto.BranchId,
            BatchNo = batchNo,
            BatchDate = dto.BatchDate,
            FiscalYearId = dto.FiscalYearId,
            Description = dto.Description,
            StatusCode = 1,
            CreationUser = username,
            CreationDate = DateTime.UtcNow
        };

        decimal totalQty = 0;
        decimal totalVal = 0;

        foreach (var l in dto.Lines)
        {
            var totalLine = l.Quantity * l.UnitCost;
            batch.Lines.Add(new InvOpeningLine
            {
                BranchId = dto.BranchId,
                WarehouseId = l.WarehouseId,
                ItemId = l.ItemId,
                BinId = l.BinId,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor,
                Quantity = l.Quantity,
                BaseQuantity = l.Quantity * l.UomFactor,
                UnitCost = l.UnitCost,
                TotalCost = totalLine,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                Notes = l.Notes
            });

            totalQty += l.Quantity * l.UomFactor;
            totalVal += totalLine;
        }

        batch.TotalQuantity = totalQty;
        batch.TotalValuationAmount = totalVal;

        var created = await _repo.CreateAsync(batch, ct);
        return ApiResponse<OpeningBatchDto>.CreateSuccess(MapToDto(created), "Opening balance batch created successfully", 201);
    }

    public async Task<ApiResponse<OpeningBatchDto>> GetBatchByIdAsync(long id, CancellationToken ct = default)
    {
        var batch = await _repo.GetByIdAsync(id, ct);
        if (batch == null)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Opening balance batch not found", null, 404);

        return ApiResponse<OpeningBatchDto>.CreateSuccess(MapToDto(batch));
    }

    public async Task<ApiResponse<PagedResultDto<OpeningBatchDto>>> GetBatchesPagedAsync(long branchId, int pageIndex, int pageSize, CancellationToken ct = default)
    {
        var (batches, total) = await _repo.GetAllAsync(branchId, pageIndex, pageSize, ct);
        var dtos = batches.Select(MapToDto).ToList();
        var pagedResult = new PagedResultDto<OpeningBatchDto>(dtos, total, pageIndex, pageSize);
        return ApiResponse<PagedResultDto<OpeningBatchDto>>.CreateSuccess(pagedResult);
    }

    public async Task<ApiResponse<OpeningBatchDto>> UpdateBatchAsync(long id, UpdateOpeningBatchDto dto, string username, CancellationToken ct = default)
    {
        var batch = await _repo.GetByIdAsync(id, ct);
        if (batch == null)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Opening balance batch not found", null, 404);

        if (batch.StatusCode != 1)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Only draft batches can be updated", null, 400);

        if (dto.BatchDate.HasValue) batch.BatchDate = dto.BatchDate.Value;
        if (dto.Description != null) batch.Description = dto.Description.Trim();

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            batch.Lines.Clear();
            decimal totalQty = 0;
            decimal totalVal = 0;

            foreach (var l in dto.Lines)
            {
                var totalLine = l.Quantity * l.UnitCost;
                batch.Lines.Add(new InvOpeningLine
                {
                    BatchId = batch.Id,
                    BranchId = batch.BranchId,
                    WarehouseId = l.WarehouseId,
                    ItemId = l.ItemId,
                    BinId = l.BinId,
                    UomCode = l.UomCode,
                    UomFactor = l.UomFactor,
                    Quantity = l.Quantity,
                    BaseQuantity = l.Quantity * l.UomFactor,
                    UnitCost = l.UnitCost,
                    TotalCost = totalLine,
                    LotNumber = l.LotNumber,
                    SerialNumber = l.SerialNumber,
                    ExpiryDate = l.ExpiryDate,
                    Notes = l.Notes
                });

                totalQty += l.Quantity * l.UomFactor;
                totalVal += totalLine;
            }

            batch.TotalQuantity = totalQty;
            batch.TotalValuationAmount = totalVal;
        }

        batch.UpdateUser = username;
        batch.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateAsync(batch, ct);
        return ApiResponse<OpeningBatchDto>.CreateSuccess(MapToDto(batch), "Opening balance batch updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteBatchAsync(long id, CancellationToken ct = default)
    {
        var batch = await _repo.GetByIdAsync(id, ct);
        if (batch == null)
            return ApiResponse<bool>.CreateFailure("Opening balance batch not found", null, 404);

        if (batch.StatusCode != 1)
            return ApiResponse<bool>.CreateFailure("Only draft batches can be deleted", null, 400);

        await _repo.DeleteAsync(id, ct);
        return ApiResponse<bool>.CreateSuccess(true, "Opening balance batch deleted successfully");
    }

    public async Task<ApiResponse<OpeningBatchDto>> PostBatchAsync(long id, string username, CancellationToken ct = default)
    {
        var batch = await _repo.GetByIdAsync(id, ct);
        if (batch == null)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Opening balance batch not found", null, 404);

        if (batch.StatusCode != 1)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Batch is already posted", null, 400);

        foreach (var line in batch.Lines)
        {
            var moveReq = new StockMovementRequestDto
            {
                ItemId = line.ItemId,
                WarehouseId = line.WarehouseId,
                BinId = line.BinId,
                Quantity = line.BaseQuantity,
                UomCode = line.UomCode,
                UnitCost = line.UnitCost,
                TransactionType = (int)TransactionType.OpeningBalance,
                LotNumber = line.LotNumber,
                SerialNumber = line.SerialNumber,
                SourceDocType = "OPB",
                SourceDocId = batch.BatchNo,
                Notes = "Opening balance ledger receipt"
            };
            await _stockLedgerService.PostMovementAsync(moveReq, ct);
        }

        batch.StatusCode = 3; // Posted
        batch.PostedAt = DateTime.UtcNow;
        batch.PostedBy = username;
        batch.UpdateUser = username;
        batch.UpdateDate = DateTime.UtcNow;

        await _repo.UpdateAsync(batch, ct);
        return ApiResponse<OpeningBatchDto>.CreateSuccess(MapToDto(batch), "Opening balance batch posted successfully");
    }

    private static OpeningBatchDto MapToDto(InvOpeningBatch entity)
    {
        return new OpeningBatchDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            BatchNo = entity.BatchNo,
            BatchDate = entity.BatchDate,
            FiscalYearId = entity.FiscalYearId,
            Description = entity.Description,
            TotalQuantity = entity.TotalQuantity,
            TotalValuationAmount = entity.TotalValuationAmount,
            StatusCode = entity.StatusCode,
            JournalEntryId = entity.JournalEntryId,
            PostedAt = entity.PostedAt,
            PostedBy = entity.PostedBy,
            Lines = entity.Lines.Select(l => new OpeningLineDto
            {
                Id = l.Id,
                WarehouseId = l.WarehouseId,
                WarehouseName = l.Warehouse?.WarehouseNameLocal,
                ItemId = l.ItemId,
                ItemCode = l.Item?.ItemCode,
                ItemName = l.Item?.ItemNameLocal,
                BinId = l.BinId,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor,
                Quantity = l.Quantity,
                BaseQuantity = l.BaseQuantity,
                UnitCost = l.UnitCost,
                TotalCost = l.TotalCost,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                Notes = l.Notes
            }).ToList()
        };
    }
}
