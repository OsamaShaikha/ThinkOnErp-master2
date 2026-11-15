using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Sales;
using ThinkOnErp.Application.Services.Inventory;

namespace ThinkOnErp.Application.Services.Sales;

public sealed class SalesDocumentService : ISalesDocumentService
{
    private readonly ITrxDocumentService _docService;

    private static readonly HashSet<int> AllowedInvoiceTrxTypes = new() { 1001, 1002, 1003 };
    private static readonly HashSet<int> AllowedReturnTrxTypes = new() { 1501, 1502 };

    public SalesDocumentService(ITrxDocumentService docService)
    {
        _docService = docService;
    }

    #region Sales Invoices (DocType 100)

    public async Task<ApiResponse<TrxDocumentDto>> CreateInvoiceAsync(CreateSalesInvoiceDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        if (!AllowedInvoiceTrxTypes.Contains(dto.TrxType))
            return ApiResponse<TrxDocumentDto>.CreateFailure($"Invalid TrxType {dto.TrxType} for Sales Invoices. Allowed values: 1001 (Cash), 1002 (Credit), 1003 (Export).", null, 400);

        if (dto.TrxType == 1002 && !dto.DueDate.HasValue)
            return ApiResponse<TrxDocumentDto>.CreateFailure("DueDate is required for credit sales invoices (TrxType 1002).", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 100, // Sales Invoice
            TrxType = dto.TrxType,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyTypeCode = 1, // Customer
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            FromWarehouseId = dto.FromWarehouseId,
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
                QuantityOut = l.Quantity,
                UnitPrice = l.UnitPrice,
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

    public async Task<ApiResponse<TrxDocumentDto>> GetInvoiceByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 100, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetInvoicesPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 100;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateInvoiceAsync(long branchId, int docYear, long id, UpdateSalesInvoiceDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyName = dto.PartyName,
            FromWarehouseId = dto.FromWarehouseId,
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
                QuantityOut = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                FromBinId = l.FromBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 100, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostInvoiceAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 100, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelInvoiceAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 100, id, username, ct);
    }

    #endregion

    #region Sales Returns (DocType 150)

    public async Task<ApiResponse<TrxDocumentDto>> CreateReturnAsync(CreateSalesReturnDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        if (!AllowedReturnTrxTypes.Contains(dto.TrxType))
            return ApiResponse<TrxDocumentDto>.CreateFailure($"Invalid TrxType {dto.TrxType} for Sales Returns. Allowed values: 1501 (Restock), 1502 (Scrap).", null, 400);

        if (dto.TrxType == 1501 && (!dto.ToWarehouseId.HasValue || dto.ToWarehouseId.Value <= 0))
            return ApiResponse<TrxDocumentDto>.CreateFailure("ToWarehouseId is required for Restock Sales Returns (TrxType 1501).", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 150, // Sales Return
            TrxType = dto.TrxType,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            PartyTypeCode = 1, // Customer
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            ToWarehouseId = dto.ToWarehouseId,
            BaseDocType = 100, // Linked to Sales Invoice
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
                QuantityIn = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetReturnByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 150, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetReturnsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 150;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateReturnAsync(long branchId, int docYear, long id, UpdateSalesReturnDto dto, string username, CancellationToken ct = default)
    {
        var internalUpdate = new UpdateTrxDocumentDto
        {
            DocDate = dto.DocDate,
            ToWarehouseId = dto.ToWarehouseId,
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
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 150, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 150, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelReturnAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 150, id, username, ct);
    }

    #endregion

    #region Sales Quotations (DocType 400)

    public async Task<ApiResponse<TrxDocumentDto>> CreateQuotationAsync(CreateSalesQuotationDto dto, string username, CancellationToken ct = default)
    {
        if (dto == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Request body cannot be null", null, 400);

        var internalDto = new CreateTrxDocumentDto
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = 400, // Quotation
            TrxType = 4001,
            DocNo = dto.DocNo,
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyTypeCode = 1, // Customer
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
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
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate
            }).ToList()
        };

        return await _docService.CreateDocumentAsync(internalDto, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetQuotationByKeyAsync(long branchId, int docYear, long id, CancellationToken ct = default)
    {
        return await _docService.GetDocumentByDocTypeAndIdAsync(branchId, docYear, 400, id, ct);
    }

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetQuotationsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        filter.DocType = 400;
        return await _docService.GetDocumentsPagedAsync(filter, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateQuotationAsync(long branchId, int docYear, long id, UpdateSalesQuotationDto dto, string username, CancellationToken ct = default)
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
                QuantityOut = l.Quantity,
                UnitPrice = l.UnitPrice,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate
            }).ToList()
        };

        return await _docService.UpdateDocumentByDocTypeAndIdAsync(branchId, docYear, 400, id, internalUpdate, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostQuotationAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.PostDocumentByDocTypeAndIdAsync(branchId, docYear, 400, id, username, ct);
    }

    public async Task<ApiResponse<TrxDocumentDto>> CancelQuotationAsync(long branchId, int docYear, long id, string username, CancellationToken ct = default)
    {
        return await _docService.CancelDocumentByDocTypeAndIdAsync(branchId, docYear, 400, id, username, ct);
    }

    #endregion
}
