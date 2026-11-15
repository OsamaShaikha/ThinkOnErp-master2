using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class InventoryMovementService : IInventoryMovementService
{
    private readonly ITrxDocumentService _docService;

    private static readonly HashSet<int> InboundVoucherTrxTypes = new() { 3001, 3002, 3003 };
    private static readonly HashSet<int> OutboundVoucherTrxTypes = new() { 3011, 3012, 3014 };

    public InventoryMovementService(ITrxDocumentService docService)
    {
        _docService = docService;
    }

    #region Stock Vouchers (DocType 300)

    public async Task<ApiResponse<TrxDocumentDto>> CreateVoucherAsync(CreateStockVoucherDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        bool isInbound = InboundVoucherTrxTypes.Contains(dto.TrxType);
        bool isOutbound = OutboundVoucherTrxTypes.Contains(dto.TrxType);

        if (!isInbound && !isOutbound)
            return ApiResponse<TrxDocumentDto>.CreateFailure($"Invalid TrxType {dto.TrxType} for Stock Vouchers. Inbound: 3001 (Opening), 3002 (Surplus), 3003 (Samples). Outbound: 3011 (Shortage), 3012 (Scrap), 3014 (Internal Use).", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 300, // Stock Voucher
            TrxType = dto.TrxType,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            PartyTypeCode = 0, // No party for internal stock vouchers
            ToWarehouseId = isInbound ? dto.WarehouseId : null,
            FromWarehouseId = isOutbound ? dto.WarehouseId : null,
            Notes = dto.Notes,
            Lines = dto.Lines.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = isInbound ? l.Quantity : 0,
                QuantityOut = isOutbound ? l.Quantity : 0,
                UnitCost = l.UnitCost,
                GlAccountCode = l.GlAccountCode ?? dto.GlAccountCode,
                ToBinId = isInbound ? l.BinId : null,
                FromBinId = isOutbound ? l.BinId : null,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetVoucherByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 300, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetVouchersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 300;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateVoucherAsync(long branchId, int docYear, long id, UpdateStockVoucherDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            FromWarehouseId = dto.WarehouseId,
            ToWarehouseId = dto.WarehouseId,
            Notes = dto.Notes,
            Lines = dto.Lines?.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = l.Quantity,
                QuantityOut = l.Quantity,
                UnitCost = l.UnitCost,
                GlAccountCode = l.GlAccountCode ?? dto.GlAccountCode,
                ToBinId = l.BinId,
                FromBinId = l.BinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 300, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostVoucherAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 300, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelVoucherAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 300, id, username, ct);
    }

    #endregion

    #region Stock Transfers (DocType 350)

    public async Task<ApiResponse<TrxDocumentDto>> CreateTransferAsync(CreateStockTransferDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        if (dto.FromWarehouseId == dto.ToWarehouseId)
            return ApiResponse<TrxDocumentDto>.CreateFailure("FromWarehouseId and ToWarehouseId must be different for stock transfers.", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 350, // Warehouse Transfer
            TrxType = 3501,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            PartyTypeCode = 0,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            Notes = dto.Notes,
            Lines = dto.Lines.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityOut = l.Quantity,
                QuantityIn = l.Quantity,
                FromBinId = l.FromBinId,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetTransferByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 350, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetTransfersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 350;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateTransferAsync(long branchId, int docYear, long id, UpdateStockTransferDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            Notes = dto.Notes,
            Lines = dto.Lines?.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityOut = l.Quantity,
                QuantityIn = l.Quantity,
                FromBinId = l.FromBinId,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 350, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostTransferAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 350, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelTransferAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 350, id, username, ct);
    }

    #endregion
}
