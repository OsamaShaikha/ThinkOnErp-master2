using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Purchasing;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.Application.Services.Purchasing;

public sealed class PurchasingDocumentService : IPurchasingDocumentService
{
    private readonly ITrxDocumentService _docService;

    private static readonly HashSet<int> AllowedBillTrxTypes = new() { 2001, 2003 };

    public PurchasingDocumentService(ITrxDocumentService docService)
    {
        _docService = docService;
    }

    #region Purchasing Bills (DocType 200)

    public async Task<ApiResponse<TrxDocumentDto>> CreateBillAsync(CreatePurchasingBillDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        if (!AllowedBillTrxTypes.Contains(dto.TrxType))
            return ApiResponse<TrxDocumentDto>.CreateFailure($"Invalid TrxType {dto.TrxType} for Purchasing Bills. Allowed values: 2001 (Local Purchase), 2003 (Import).", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 200, // Purchasing Bill
            TrxType = dto.TrxType,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyTypeCode = 2, // Vendor
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            ToWarehouseId = dto.ToWarehouseId,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate,
            PaymentMethodCode = dto.PaymentMethodCode,
            DiscountAmount = dto.DiscountAmount,
            BaseDocType = dto.BaseDocType,
            BaseDocId = dto.BaseDocId,
            Notes = dto.Notes,
            Lines = dto.Lines.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetBillByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 200, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetBillsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 200;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateBillAsync(long branchId, int docYear, long id, UpdatePurchasingBillDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyName = dto.PartyName,
            ToWarehouseId = dto.ToWarehouseId,
            PaymentMethodCode = dto.PaymentMethodCode,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Lines = dto.Lines?.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 200, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostBillAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 200, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelBillAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 200, id, username, ct);
    }

    #endregion

    #region Purchasing Returns (DocType 250)

    public async Task<ApiResponse<TrxDocumentDto>> CreateReturnAsync(CreatePurchasingReturnDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 250, // Purchasing Return
            TrxType = 2501,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            PartyTypeCode = 2, // Vendor
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            FromWarehouseId = dto.FromWarehouseId,
            BaseDocType = 200, // Linked to Purchasing Bill
            BaseDocId = dto.BaseDocId,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Lines = dto.Lines.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityOut = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                FromBinId = l.FromBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetReturnByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 250, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetReturnsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 250;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateReturnAsync(long branchId, int docYear, long id, UpdatePurchasingReturnDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            FromWarehouseId = dto.FromWarehouseId,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Lines = dto.Lines?.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityOut = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                FromBinId = l.FromBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 250, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 250, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 250, id, username, ct);
    }

    #endregion

    #region Purchase Orders (DocType 450)

    public async Task<ApiResponse<TrxDocumentDto>> CreateOrderAsync(CreatePurchaseOrderDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 450, // Purchase Order
            TrxType = 4004,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyTypeCode = 2, // Vendor
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            CurrencyCode = dto.CurrencyCode,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Lines = dto.Lines.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetOrderByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 450, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetOrdersPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 450;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateOrderAsync(long branchId, int docYear, long id, UpdatePurchaseOrderDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyName = dto.PartyName,
            DiscountAmount = dto.DiscountAmount,
            Notes = dto.Notes,
            Lines = dto.Lines?.Select(l => new CreateTrxDocumentLineDto
            {
                ItemId = l.ItemId,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor > 0 ? l.UomFactor : 1,
                Quantity = l.Quantity,
                QuantityIn = l.Quantity,
                UnitCost = l.UnitCost,
                UnitPrice = l.UnitCost,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 450, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostOrderAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 450, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelOrderAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 450, id, username, ct);
    }

    #endregion
}
