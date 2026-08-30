using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Bom;

public sealed class UpdateInvBomDto
{
    public string? BomNameAr { get; set; }
    public string? BomNameEn { get; set; }
    public decimal? OutputQty { get; set; }
    public string? UomCode { get; set; }
    public BomType? BomType { get; set; }
    public decimal? LaborCost { get; set; }
    public decimal? OverheadCost { get; set; }
    public bool? IsDefault { get; set; }
    public string? Notes { get; set; }
    public List<CreateInvBomLineDto>? Lines { get; set; }
}
