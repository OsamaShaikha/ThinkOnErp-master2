using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Documents;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class TrxDocumentMapper
{
    public static TrxDocumentHeader ToEntity(CreateTrxDocumentDto dto, long id, string username)
    {
        var header = new TrxDocumentHeader
        {
            BranchId = dto.BranchId,
            DocYear = dto.DocYear,
            DocType = dto.DocType,
            Id = id,
            TrxType = dto.TrxType,
            DocNo = dto.DocNo ?? $"{dto.DocType}-{dto.DocYear}-{id:D6}",
            DocDate = dto.DocDate,
            DueDate = dto.DueDate,
            PartyTypeCode = dto.PartyTypeCode,
            PartyId = dto.PartyId,
            PartyName = dto.PartyName,
            FromWarehouseId = dto.FromWarehouseId,
            ToWarehouseId = dto.ToWarehouseId,
            CurrencyCode = dto.CurrencyCode,
            ExchangeRate = dto.ExchangeRate,
            PaymentMethodCode = dto.PaymentMethodCode,
            DiscountAmount = dto.DiscountAmount,
            BaseBranchId = dto.BaseBranchId,
            BaseDocYear = dto.BaseDocYear,
            BaseDocType = dto.BaseDocType,
            BaseDocId = dto.BaseDocId,
            Notes = dto.Notes,
            CreationUser = username,
            CreationDate = System.DateTime.UtcNow,
            DocStatusCode = 1 // Draft
        };

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

            header.Lines.Add(new TrxDocumentLine
            {
                BranchId = dto.BranchId,
                DocYear = dto.DocYear,
                DocType = dto.DocType,
                DocId = id,
                LineNo = lineNo++,
                TrxType = dto.TrxType,
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

        header.TotalGross = gross;
        header.TotalNetBeforeTax = gross - dto.DiscountAmount;
        header.TaxAmount = totalTax;
        header.TotalNet = header.TotalNetBeforeTax + totalTax;
        header.TotalCost = totalCost;
        header.TotalProfit = header.TotalNetBeforeTax - totalCost;
        header.ProfitMarginPercent = header.TotalNetBeforeTax > 0 ? Math.Round((header.TotalProfit / header.TotalNetBeforeTax) * 100m, 4) : 0;
        header.RemainingAmount = header.TotalNet - header.PaidAmount;

        return header;
    }

    public static TrxDocumentDto ToDto(TrxDocumentHeader entity)
    {
        return new TrxDocumentDto
        {
            BranchId = entity.BranchId,
            DocYear = entity.DocYear,
            DocType = entity.DocType,
            Id = entity.Id,
            TrxType = entity.TrxType,
            DocTypeName = entity.DocTypeConfig?.TypeNameLocal ?? entity.DocType.ToString(),
            TrxTypeName = entity.TrxTypeConfig?.TrxNameLocal ?? entity.TrxType.ToString(),
            DocNo = entity.DocNo,
            DocDate = entity.DocDate,
            DueDate = entity.DueDate,
            PartyTypeCode = entity.PartyTypeCode,
            PartyId = entity.PartyId,
            PartyName = entity.PartyName,
            FromWarehouseId = entity.FromWarehouseId,
            FromWarehouseName = entity.FromWarehouse?.WarehouseNameLocal,
            ToWarehouseId = entity.ToWarehouseId,
            ToWarehouseName = entity.ToWarehouse?.WarehouseNameLocal,
            CurrencyCode = entity.CurrencyCode,
            ExchangeRate = entity.ExchangeRate,
            PaymentMethodCode = entity.PaymentMethodCode,
            TotalGross = entity.TotalGross,
            DiscountAmount = entity.DiscountAmount,
            TotalNetBeforeTax = entity.TotalNetBeforeTax,
            TaxAmount = entity.TaxAmount,
            TotalNet = entity.TotalNet,
            TotalCost = entity.TotalCost,
            TotalProfit = entity.TotalProfit,
            ProfitMarginPercent = entity.ProfitMarginPercent,
            PaidAmount = entity.PaidAmount,
            RemainingAmount = entity.RemainingAmount,
            BaseBranchId = entity.BaseBranchId,
            BaseDocYear = entity.BaseDocYear,
            BaseDocType = entity.BaseDocType,
            BaseDocId = entity.BaseDocId,
            DocStatusCode = entity.DocStatusCode,
            IsPostedGl = entity.IsPostedGl,
            IsPostedStock = entity.IsPostedStock,
            JournalEntryId = entity.JournalEntryId,
            Notes = entity.Notes,
            CreationUser = entity.CreationUser,
            CreationDate = entity.CreationDate,
            PostedBy = entity.PostedBy,
            PostedDate = entity.PostedDate,
            Lines = entity.Lines.Select(l => new TrxDocumentLineDto
            {
                LineNo = l.LineNo,
                TrxType = l.TrxType,
                ItemId = l.ItemId,
                ItemCode = l.Item?.ItemCode ?? string.Empty,
                ItemName = l.Item?.ItemNameLocal ?? string.Empty,
                ItemDescription = l.ItemDescription,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor,
                QuantityIn = l.QuantityIn,
                QuantityOut = l.QuantityOut,
                BaseQuantityIn = l.BaseQuantityIn,
                BaseQuantityOut = l.BaseQuantityOut,
                UnitPrice = l.UnitPrice,
                UnitCost = l.UnitCost,
                LineCost = l.LineCost,
                LineProfit = l.LineProfit,
                ProfitMarginPercent = l.ProfitMarginPercent,
                DiscountPercent = l.DiscountPercent,
                DiscountAmount = l.DiscountAmount,
                TaxRate = l.TaxRate,
                TaxAmount = l.TaxAmount,
                LineTotal = l.LineTotal,
                FromBinId = l.FromBinId,
                ToBinId = l.ToBinId,
                LotNumber = l.LotNumber,
                SerialNumber = l.SerialNumber,
                ExpiryDate = l.ExpiryDate,
                GlAccountCode = l.GlAccountCode,
                BaseLineNo = l.BaseLineNo
            }).ToList()
        };
    }
}
