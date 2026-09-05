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

        var docMonth = dto.DocDate.Month;
        var nextSerial = await _docRepository.GenerateNextSerialNoAsync(dto.BranchId, dto.DocYear, docMonth, dto.DocType, docType.ResetPolicy, ct);
        var docNo = $"{docType.DocPrefix}{dto.DocYear}-{nextSerial:D6}";

        var entity = TrxDocumentMapper.ToEntity(dto, nextSerial, username);
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

    public async Task<ApiResponse<PagedResultDto<TrxDocumentDto>>> GetDocumentsPagedAsync(TrxDocumentFilterDto filter, CancellationToken ct = default)
    {
        var (items, total) = await _docRepository.GetPagedAsync(
            filter.BranchId, filter.DocYear, filter.DocType, filter.TrxType, filter.PartyTypeCode, filter.PartyId, filter.StatusCode,
            filter.PageNumber, filter.PageSize, ct);

        var dtos = items.Select(TrxDocumentMapper.ToDto).ToList();
        var pagedResult = new PagedResultDto<TrxDocumentDto>(dtos, total, filter.PageNumber, filter.PageSize);
        return ApiResponse<PagedResultDto<TrxDocumentDto>>.CreateSuccess(pagedResult);
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
            decimal totalCost = 0;

            foreach (var l in dto.Lines)
            {
                var baseQtyIn = l.QuantityIn * l.UomFactor;
                var baseQtyOut = l.QuantityOut * l.UomFactor;
                var activeQty = l.QuantityIn > 0 ? l.QuantityIn : l.QuantityOut;
                var lineGross = activeQty * l.UnitPrice;
                var lineNet = lineGross - l.DiscountAmount;
                var taxAmt = lineNet * (l.TaxRate / 100m);
                var lineTotal = lineNet + taxAmt;

                var activeCostQty = l.QuantityOut > 0 ? l.QuantityOut : (l.QuantityIn > 0 ? l.QuantityIn : 0);
                var lineCost = activeCostQty * l.UnitCost;
                var lineProfit = l.QuantityOut > 0 ? (lineNet - lineCost) : 0;
                var marginPct = (l.QuantityOut > 0 && lineNet > 0) ? (lineProfit / lineNet) * 100m : 0;

                gross += lineGross;
                totalTax += taxAmt;
                if (l.QuantityOut > 0) totalCost += lineCost;

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
                    LineCost = lineCost,
                    LineProfit = lineProfit,
                    ProfitMarginPercent = Math.Round(marginPct, 4),
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
            doc.TotalCost = totalCost;
            doc.TotalProfit = doc.TotalNetBeforeTax - totalCost;
            doc.ProfitMarginPercent = doc.TotalNetBeforeTax > 0 ? Math.Round((doc.TotalProfit / doc.TotalNetBeforeTax) * 100m, 4) : 0;
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
        decimal totalCost = 0;

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
                            var moveResp = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                            if (moveResp.Success && moveResp.Data != null)
                            {
                                line.UnitCost += (moveResp.Data.UnitCost * comp.Quantity);
                            }
                        }
                        line.LineCost = line.QuantityOut * line.UnitCost;
                        var lineNet = (line.QuantityOut * line.UnitPrice) - line.DiscountAmount;
                        line.LineProfit = lineNet - line.LineCost;
                        line.ProfitMarginPercent = lineNet > 0 ? Math.Round((line.LineProfit / lineNet) * 100m, 4) : 0;
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
                    var moveResp = await _stockLedgerService.PostMovementAsync(moveReq, ct);
                    if (moveResp.Success && moveResp.Data != null && moveResp.Data.UnitCost > 0)
                    {
                        line.UnitCost = moveResp.Data.UnitCost;
                        line.LineCost = (line.QuantityOut > 0 ? line.QuantityOut : line.QuantityIn) * line.UnitCost;
                        if (line.QuantityOut > 0)
                        {
                            var lineNet = (line.QuantityOut * line.UnitPrice) - line.DiscountAmount;
                            line.LineProfit = lineNet - line.LineCost;
                            line.ProfitMarginPercent = lineNet > 0 ? Math.Round((line.LineProfit / lineNet) * 100m, 4) : 0;
                        }
                    }
                }

                if (line.QuantityOut > 0) totalCost += line.LineCost;
            }
            doc.IsPostedStock = true;
        }

        if (totalCost > 0 || doc.TotalCost == 0)
        {
            doc.TotalCost = totalCost > 0 ? totalCost : doc.Lines.Where(l => l.QuantityOut > 0).Sum(l => l.LineCost);
            doc.TotalProfit = doc.TotalNetBeforeTax - doc.TotalCost;
            doc.ProfitMarginPercent = doc.TotalNetBeforeTax > 0 ? Math.Round((doc.TotalProfit / doc.TotalNetBeforeTax) * 100m, 4) : 0;
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

    public async Task<ApiResponse<ProfitabilitySummaryReportDto>> GetProfitabilityReportAsync(
        long? branchId, int? docYear, DateTime? fromDate, DateTime? toDate, string? customerCode, long? itemId, CancellationToken ct = default)
    {
        var rows = await _docRepository.GetProfitabilityReportFromViewAsync(
            branchId, docYear, fromDate, toDate, customerCode, itemId, ct);

        var dtoList = rows.Select(r => new SalesInvoiceProfitabilityDto
        {
            BranchId = r.BranchId,
            DocYear = r.DocYear,
            DocType = r.DocType,
            DocId = r.DocId,
            DocNo = r.DocNo,
            DocDate = r.DocDate,
            DocStatusCode = r.DocStatusCode,
            IsPostedGl = r.IsPostedGl,
            IsPostedStock = r.IsPostedStock,
            CustomerId = r.CustomerId,
            CustomerCode = r.CustomerCode,
            CustomerName = r.CustomerName,
            FromWarehouseId = r.FromWarehouseId,
            HeaderTotalGross = r.HeaderTotalGross,
            HeaderTotalNet = r.HeaderTotalNet,
            HeaderTotalCost = r.HeaderTotalCost,
            HeaderTotalProfit = r.HeaderTotalProfit,
            HeaderProfitMargin = r.HeaderProfitMargin,
            LineNo = r.LineNo,
            ItemId = r.ItemId,
            ItemCode = r.ItemCode,
            ItemName = r.ItemName,
            Quantity = r.Quantity,
            BaseQuantity = r.BaseQuantity,
            UnitPrice = r.UnitPrice,
            UnitCost = r.UnitCost,
            LineTotal = r.LineTotal,
            LineCost = r.LineCost,
            LineProfit = r.LineProfit,
            LineProfitMargin = r.LineProfitMargin
        }).ToList();

        var totalRev = dtoList.Sum(x => x.LineTotal);
        var totalCost = dtoList.Sum(x => x.LineCost);
        var totalProfit = totalRev - totalCost;
        var marginPct = totalRev > 0 ? Math.Round((totalProfit / totalRev) * 100m, 4) : 0;
        var distinctInvoices = dtoList.Select(x => x.DocId).Distinct().Count();

        var summary = new ProfitabilitySummaryReportDto
        {
            TotalRevenue = totalRev,
            TotalCost = totalCost,
            TotalGrossProfit = totalProfit,
            OverallProfitMarginPercent = marginPct,
            TotalInvoicesCount = distinctInvoices,
            TotalLinesCount = dtoList.Count,
            Items = dtoList
        };

        return ApiResponse<ProfitabilitySummaryReportDto>.CreateSuccess(summary, "Profitability report generated successfully");
    }
}
