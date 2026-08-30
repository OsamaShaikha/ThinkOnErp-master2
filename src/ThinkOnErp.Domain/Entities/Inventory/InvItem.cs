using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItem
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameAr { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public long MainGroupId { get; set; }
    public long? SubGroupId { get; set; }
    public ItemType ItemType { get; set; }
    public string UomBase { get; set; } = string.Empty;
    public CostingMethod CostingMethod { get; set; }
    public decimal StandardCost { get; set; }
    
    public bool SerialTracking { get; set; }
    public bool LotTracking { get; set; }
    public bool ExpiryTracking { get; set; }
    public int? ShelfLifeDays { get; set; }
    
    public bool AllowNegativeStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQty { get; set; }
    public int LeadTimeDays { get; set; }
    
    public decimal Weight { get; set; }
    public string? WeightUnit { get; set; }
    
    public string? GlControlAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    
    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }
    
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItemGroup? MainGroup { get; set; }
    public InvItemGroup? SubGroup { get; set; }
    public List<InvItemUomConversion> UomConversions { get; set; } = new();
    public List<InvItemBarcode> Barcodes { get; set; } = new();
    public List<InvStockBalance> StockBalances { get; set; } = new();
    public List<InvLotMaster> Lots { get; set; } = new();
    public List<InvSerialMaster> Serials { get; set; } = new();
    public List<InvBomHeader> AssembledBoms { get; set; } = new();
}
