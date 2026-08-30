using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Bom;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvBomMapper
{
    public static InvBomHeader ToEntity(CreateInvBomDto dto, string username)
    {
        var header = new InvBomHeader
        {
            BranchId = dto.BranchId,
            BomCode = dto.BomCode,
            BomNameAr = dto.BomNameAr,
            BomNameEn = dto.BomNameEn,
            ParentItemId = dto.ParentItemId,
            OutputQty = dto.OutputQty,
            UomCode = dto.UomCode,
            BomType = dto.BomType,
            LaborCost = dto.LaborCost,
            OverheadCost = dto.OverheadCost,
            IsDefault = dto.IsDefault,
            Notes = dto.Notes,
            CreationUser = username,
            CreationDate = System.DateTime.UtcNow,
            IsActive = true
        };

        int lineNo = 1;
        foreach (var l in dto.Lines)
        {
            header.Lines.Add(new InvBomLine
            {
                LineNo = lineNo++,
                ComponentItemId = l.ComponentItemId,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor,
                Quantity = l.Quantity,
                ScrapPercent = l.ScrapPercent,
                CostSharePercent = l.CostSharePercent,
                AllowSubstitute = l.AllowSubstitute,
                SubstituteItemId = l.SubstituteItemId,
                Notes = l.Notes
            });
        }

        return header;
    }

    public static InvBomDto ToDto(InvBomHeader entity)
    {
        return new InvBomDto
        {
            Id = entity.Id,
            BranchId = entity.BranchId,
            BomCode = entity.BomCode,
            BomNameAr = entity.BomNameAr,
            BomNameEn = entity.BomNameEn,
            ParentItemId = entity.ParentItemId,
            ParentItemCode = entity.ParentItem?.ItemCode ?? string.Empty,
            ParentItemName = entity.ParentItem?.ItemNameAr ?? string.Empty,
            OutputQty = entity.OutputQty,
            UomCode = entity.UomCode,
            BomType = entity.BomType,
            LaborCost = entity.LaborCost,
            OverheadCost = entity.OverheadCost,
            IsDefault = entity.IsDefault,
            IsActive = entity.IsActive,
            Notes = entity.Notes,
            Lines = entity.Lines.Select(l => new InvBomLineDto
            {
                LineNo = l.LineNo,
                ComponentItemId = l.ComponentItemId,
                ComponentItemCode = l.ComponentItem?.ItemCode ?? string.Empty,
                ComponentItemName = l.ComponentItem?.ItemNameAr ?? string.Empty,
                UomCode = l.UomCode,
                UomFactor = l.UomFactor,
                Quantity = l.Quantity,
                ScrapPercent = l.ScrapPercent,
                CostSharePercent = l.CostSharePercent,
                AllowSubstitute = l.AllowSubstitute,
                SubstituteItemId = l.SubstituteItemId,
                SubstituteItemName = l.SubstituteItem?.ItemNameAr,
                Notes = l.Notes
            }).ToList()
        };
    }
}
