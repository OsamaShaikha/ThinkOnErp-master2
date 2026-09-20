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
    private readonly IInvLotSerialRepository _lotSerialRepository;
    private readonly ILogger<InvOpeningBalanceService> _logger;

    public InvOpeningBalanceService(
        IInvOpeningBalanceRepository repo,
        IInvStockLedgerService stockLedgerService,
        IInvLotSerialRepository lotSerialRepository,
        ILogger<InvOpeningBalanceService> logger)
    {
        _repo = repo;
        _stockLedgerService = stockLedgerService;
        _lotSerialRepository = lotSerialRepository;
        _logger = logger;
    }

    public async Task<ApiResponse<OpeningBatchDto>> CreateBatchAsync(CreateOpeningBatchDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Request body cannot be null", null, 400);

        dto.Lines ??= new();

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
            var serialsList = new List<string>();
            if (l.SerialNumbers != null && l.SerialNumbers.Count > 0)
            {
                serialsList.AddRange(l.SerialNumbers.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
            }
            else if (!string.IsNullOrWhiteSpace(l.SerialNumber))
            {
                serialsList.AddRange(l.SerialNumber.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
            }

            if (l.Quantity <= 0 && serialsList.Count > 0)
            {
                l.Quantity = serialsList.Count;
            }

            if (serialsList.Count > 1)
            {
                foreach (var sn in serialsList)
                {
                    const decimal lineQty = 1m;
                    var totalLine = lineQty * l.UnitCost;
                    batch.Lines.Add(new InvOpeningLine
                    {
                        BranchId = dto.BranchId,
                        WarehouseId = l.WarehouseId,
                        ItemId = l.ItemId,
                        BinId = l.BinId,
                        UomCode = l.UomCode,
                        UomFactor = l.UomFactor,
                        Quantity = lineQty,
                        BaseQuantity = lineQty * l.UomFactor,
                        UnitCost = l.UnitCost,
                        TotalCost = totalLine,
                        LotNumber = l.LotNumber,
                        SerialNumber = sn,
                        ExpiryDate = l.ExpiryDate,
                        Notes = l.Notes
                    });

                    totalQty += lineQty * l.UomFactor;
                    totalVal += totalLine;
                }
            }
            else
            {
                var sn = serialsList.FirstOrDefault() ?? l.SerialNumber?.Trim();
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
                    SerialNumber = sn,
                    ExpiryDate = l.ExpiryDate,
                    Notes = l.Notes
                });

                totalQty += l.Quantity * l.UomFactor;
                totalVal += totalLine;
            }
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
        if (dto == null)
            return ApiResponse<OpeningBatchDto>.CreateFailure("Request body cannot be null", null, 400);

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
                var serialsList = new List<string>();
                if (l.SerialNumbers != null && l.SerialNumbers.Count > 0)
                {
                    serialsList.AddRange(l.SerialNumbers.Where(s => !string.IsNullOrWhiteSpace(s)).Select(s => s.Trim()));
                }
                else if (!string.IsNullOrWhiteSpace(l.SerialNumber))
                {
                    serialsList.AddRange(l.SerialNumber.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()));
                }

                if (l.Quantity <= 0 && serialsList.Count > 0)
                {
                    l.Quantity = serialsList.Count;
                }

                if (serialsList.Count > 1)
                {
                    foreach (var sn in serialsList)
                    {
                        const decimal lineQty = 1m;
                        var totalLine = lineQty * l.UnitCost;
                        batch.Lines.Add(new InvOpeningLine
                        {
                            BatchId = batch.Id,
                            BranchId = batch.BranchId,
                            WarehouseId = l.WarehouseId,
                            ItemId = l.ItemId,
                            BinId = l.BinId,
                            UomCode = l.UomCode,
                            UomFactor = l.UomFactor,
                            Quantity = lineQty,
                            BaseQuantity = lineQty * l.UomFactor,
                            UnitCost = l.UnitCost,
                            TotalCost = totalLine,
                            LotNumber = l.LotNumber,
                            SerialNumber = sn,
                            ExpiryDate = l.ExpiryDate,
                            Notes = l.Notes
                        });

                        totalQty += lineQty * l.UomFactor;
                        totalVal += totalLine;
                    }
                }
                else
                {
                    var sn = serialsList.FirstOrDefault() ?? l.SerialNumber?.Trim();
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
                        SerialNumber = sn,
                        ExpiryDate = l.ExpiryDate,
                        Notes = l.Notes
                    });

                    totalQty += l.Quantity * l.UomFactor;
                    totalVal += totalLine;
                }
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
            if (!string.IsNullOrWhiteSpace(line.SerialNumber))
            {
                var sn = line.SerialNumber.Trim();
                var exists = await _lotSerialRepository.ExistsSerialNumberAsync(line.ItemId, sn, ct);
                if (!exists)
                {
                    await _lotSerialRepository.AddSerialAsync(new InvSerialMaster
                    {
                        ItemId = line.ItemId,
                        SerialNumber = sn,
                        Status = SerialStatus.Available,
                        CurrentWarehouseId = line.WarehouseId,
                        CurrentBinId = line.BinId
                    }, ct);
                }
            }

            if (!string.IsNullOrWhiteSpace(line.LotNumber))
            {
                var lot = await _lotSerialRepository.GetByLotNumberAsync(line.ItemId, line.LotNumber.Trim(), ct);
                if (lot == null)
                {
                    await _lotSerialRepository.AddLotAsync(new InvLotMaster
                    {
                        ItemId = line.ItemId,
                        LotNumber = line.LotNumber.Trim(),
                        Status = LotStatus.Active,
                        ExpiryDate = line.ExpiryDate
                    }, ct);
                }
            }

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

        await _lotSerialRepository.SaveChangesAsync(ct);

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
                SerialNumbers = !string.IsNullOrWhiteSpace(l.SerialNumber)
                    ? new List<string> { l.SerialNumber }
                    : new List<string>(),
                ExpiryDate = l.ExpiryDate,
                Notes = l.Notes
            }).ToList()
        };
    }
}
