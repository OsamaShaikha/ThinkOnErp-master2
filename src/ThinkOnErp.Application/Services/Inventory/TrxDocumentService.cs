using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using ThinkOnErp.Application.Common;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Application.DTOs.Inventory.StockMovements;
using ThinkOnErp.Application.Mappings.Inventory;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Inventory.Enums;
using ThinkOnErp.Domain.Interfaces.Inventory;

namespace ThinkOnErp.Application.Services.Inventory;

public sealed class TrxDocumentService : ITrxDocumentService
{
    private readonly ITrxDocumentRepository _docRepository;
    private readonly ITrxTypeRepository _typeRepository;
    private readonly IInvBomRepository _bomRepository;
    private readonly IInvItemRepository _itemRepository;
    private readonly IInvStockLedgerService _stockLedgerService;
    private readonly ILogger<TrxDocumentService> _logger;

    public TrxDocumentService(
        ITrxDocumentRepository docRepository,
        ITrxTypeRepository typeRepository,
        IInvBomRepository bomRepository,
        IInvItemRepository itemRepository,
        IInvStockLedgerService stockLedgerService,
        ILogger<TrxDocumentService> logger)
    {
        _docRepository = docRepository;
        _typeRepository = typeRepository;
        _bomRepository = bomRepository;
        _itemRepository = itemRepository;
        _stockLedgerService = stockLedgerService;
        _logger = logger;
    }

    public async Task<ApiResponse<TrxDocumentDto>> CreateDocumentAsync(CreateTrxDocumentDto dto, string username, CancellationToken ct = default)
    {
        var docType = await _typeRepository.GetDocTypeAsync(dto.DocType, ct);
        var trxType = await _typeRepository.GetTrxTypeAsync(dto.TrxType, ct);

        if (docType == null || trxType == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Invalid document type or transaction type", null, 400);

        var nextId = await _docRepository.GetNextIdAsync(dto.BranchId, dto.DocYear, dto.DocType, ct);
        var docNo = $"{docType.DocPrefix}{dto.DocYear}-{nextId:D6}";

        var entity = TrxDocumentMapper.ToEntity(dto, nextId, username);
        entity.DocNo = docNo;

        var created = await _docRepository.CreateAsync(entity, ct);
        return ApiResponse<TrxDocumentDto>.CreateSuccess(TrxDocumentMapper.ToDto(created), "Document created successfully", 201);
    }

    public async Task<ApiResponse<TrxDocumentDto>> GetDocumentByKeyAsync(long branchId, int docYear, int docType, long id, CancellationToken ct = default)
    {
        var doc = await _docRepository.GetByKeyAsync(branchId, docYear, docType, id, ct);
        if (doc == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Document not found", null, 404);

        return ApiResponse<TrxDocumentDto>.CreateSuccess(TrxDocumentMapper.ToDto(doc));
    }

    public async Task<ApiResponse<(List<TrxDocumentDto> Items, int TotalCount)>> GetDocumentsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        var (items, total) = await _docRepository.GetPagedAsync(
            filter.BranchId, filter.DocYear, filter.DocType, filter.TrxType, filter.PartyTypeCode, filter.PartyId, filter.StatusCode,
            filter.PageNumber, filter.PageSize, ct);

        var dtos = items.Select(TrxDocumentMapper.ToDto).ToList();
        return ApiResponse<(List<TrxDocumentDto> Items, int TotalCount)>.CreateSuccess((dtos, total));
    }

    public async Task<ApiResponse<TrxDocumentDto>> UpdateDocumentAsync(long branchId, int docYear, int docType, long id, UpdateTrxDocumentDto dto, string username, CancellationToken ct = default)
    {
        var doc = await _docRepository.GetByKeyAsync(branchId, docYear, docType, id, ct);
        if (doc == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Document not found", null, 404);

        if (doc.DocStatusCode != 1)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Only draft documents can be updated", null, 400);

        if (dto.DocDate.HasValue) doc.DocDate = dto.DocDate.Value;
        if (dto.DueDate.HasValue) doc.DueDate = dto.DueDate.Value;
        if (dto.PartyName != null) doc.PartyName = dto.PartyName.Trim();
        if (dto.FromWarehouseId.HasValue) doc.FromWarehouseId = dto.FromWarehouseId;
        if (dto.ToWarehouseId.HasValue) doc.ToWarehouseId = dto.ToWarehouseId;
        if (dto.PaymentMethodCode.HasValue) doc.PaymentMethodCode = dto.PaymentMethodCode.Value;
        if (dto.DiscountAmount.HasValue) doc.DiscountAmount = dto.DiscountAmount.Value;
        if (dto.Notes != null) doc.Notes = dto.Notes.Trim();

        if (dto.Lines != null && dto.Lines.Count > 0)
        {
            doc.Lines.Clear();
            int lineNo = 1;
            decimal gross = 0;
            decimal totalTax = 0;

            foreach (var l in dto.Lines)
            {
                var baseQtyIn = l.QuantityIn * l.UomFactor;
                var baseQtyOut = l.QuantityOut * l.UomFactor;
                var activeQty = l.QuantityIn > 0 ? l.QuantityIn : l.QuantityOut;
                var lineGross = activeQty * l.UnitPrice;
                var lineNet = lineGross - l.DiscountAmount;
                var taxAmt = lineNet * (l.TaxRate / 100m);
                var lineTotal = lineNet + taxAmt;

                gross += lineGross;
                totalTax += taxAmt;

                doc.Lines.Add(new TrxDocumentLine
                {
                    BranchId = doc.BranchId,
                    DocYear = doc.DocYear,
                    DocType = doc.DocType,
                    DocId = doc.Id,
                    LineNo = lineNo++,
                    TrxType = doc.TrxType,
                    ItemId = l.ItemId,
                    ItemDescription = l.ItemDescription,
                    UomCode = l.UomCode,
                    UomFactor = l.UomFactor,
                    QuantityIn = l.QuantityIn,
                    QuantityOut = l.QuantityOut,
                    BaseQuantityIn = baseQtyIn,
                    BaseQuantityOut = baseQtyOut,
                    UnitPrice = l.UnitPrice,
                    UnitCost = l.UnitCost,
                    DiscountPercent = l.DiscountPercent,
                    DiscountAmount = l.DiscountAmount,
                    TaxRate = l.TaxRate,
                    TaxAmount = taxAmt,
                    LineTotal = lineTotal,
                    FromBinId = l.FromBinId,
                    ToBinId = l.ToBinId,
                    LotNumber = l.LotNumber,
                    SerialNumber = l.SerialNumber,
                    ExpiryDate = l.ExpiryDate,
                    GlAccountCode = l.GlAccountCode,
                    BaseLineNo = l.BaseLineNo
                });
            }

            doc.TotalGross = gross;
            doc.TotalNetBeforeTax = gross - doc.DiscountAmount;
            doc.TaxAmount = totalTax;
            doc.TotalNet = doc.TotalNetBeforeTax + totalTax;
            doc.RemainingAmount = doc.TotalNet - doc.PaidAmount;
        }

        doc.UpdateUser = username;
        doc.UpdateDate = DateTime.UtcNow;

        await _docRepository.UpdateAsync(doc, ct);
        return ApiResponse<TrxDocumentDto>.CreateSuccess(TrxDocumentMapper.ToDto(doc), "Document updated successfully");
    }

    public async Task<ApiResponse<bool>> DeleteDocumentAsync(long branchId, int docYear, int docType, long id, string username, CancellationToken ct = default)
    {
        var doc = await _docRepository.GetByKeyAsync(branchId, docYear, docType, id, ct);
        if (doc == null)
            return ApiResponse<bool>.CreateFailure("Document not found", null, 404);

        if (doc.DocStatusCode != 1)
            return ApiResponse<bool>.CreateFailure("Only draft documents can be deleted", null, 400);

        await _docRepository.DeleteAsync(doc, ct);
        return ApiResponse<bool>.CreateSuccess(true, "Document deleted successfully");
    }

    public async Task<ApiResponse<TrxDocumentDto>> PostDocumentAsync(long branchId, int docYear, int docType, long id, string username, CancellationToken ct = default)
    {
        var doc = await _docRepository.GetByKeyAsync(branchId, docYear, docType, id, ct);
        if (doc == null)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Document not found", null, 404);

        if (doc.DocStatusCode != 1)
            return ApiResponse<TrxDocumentDto>.CreateFailure("Only draft documents can be posted", null, 400);

        var trxType = await _typeRepository.GetTrxTypeAsync(doc.TrxType, ct);
        if (trxType != null && trxType.AffectsStock)
        {
            foreach (var line in doc.Lines)
            {
                var item = await _itemRepository.GetByIdAsync(line.ItemId, ct);
                if (item != null && item.ItemType == ItemType.Kit)
                {
                    var bom = await _bomRepository.GetDefaultByParentItemIdAsync(item.Id, ct);
                    if (bom != null && bom.Lines.Count > 0)
                    {
                        foreach (var comp in bom.Lines)
                        {
                            var moveReq = new StockMovementRequestDto
                            {
                                ItemId = comp.ComponentItemId,
                                WarehouseId = doc.FromWarehouseId ?? 1,
                                Quantity = line.QuantityOut * comp.Quantity,
                                UomCode = comp.UomCode,
                                TransactionType = (int)TransactionType.SalesIssue,
                                SourceDocId = doc.DocNo,
                                Notes = $"Auto BOM explosion for {item.ItemCode}"
                            };
                            await _stockLedgerService.PostMovementAsync(moveReq, ct);
                        }
                    }
                }
                else
                {
                    var whId = line.QuantityIn > 0 ? (doc.ToWarehouseId ?? 1) : (doc.FromWarehouseId ?? 1);
                    var moveReq = new StockMovementRequestDto
                    {
                        ItemId = line.ItemId,
                        WarehouseId = whId,
                        Quantity = line.QuantityIn > 0 ? line.QuantityIn : line.QuantityOut,
                        UomCode = line.UomCode,
                        UnitCost = line.UnitCost,
                        TransactionType = line.QuantityIn > 0 ? (int)TransactionType.GrnReceipt : (int)TransactionType.SalesIssue,
                        LotNumber = line.LotNumber,
                        SerialNumber = line.SerialNumber,
                        SourceDocId = doc.DocNo
                    };
                    await _stockLedgerService.PostMovementAsync(moveReq, ct);
                }
            }
            doc.IsPostedStock = true;
        }

        doc.DocStatusCode = 3; // Posted
        doc.IsPostedGl = true;
        doc.PostedBy = username;
        doc.PostedDate = DateTime.UtcNow;
        doc.UpdateUser = username;
        doc.UpdateDate = DateTime.UtcNow;

        await _docRepository.UpdateAsync(doc, ct);
        return ApiResponse<TrxDocumentDto>.CreateSuccess(TrxDocumentMapper.ToDto(doc), "Document posted successfully");
    }
}
