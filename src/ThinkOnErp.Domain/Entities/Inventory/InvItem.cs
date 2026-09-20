using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public sealed class InvItem
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string? Sku { get; set; }
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public long CategoryId { get; set; }
    
    #region Backward Compatibility Aliases
    public long MainCategoryId
    {
        get => CategoryId;
        set => CategoryId = value;
    }
    public long MainGroupId
    {
        get => CategoryId;
        set => CategoryId = value;
    }
    public long? SubCategoryId
    {
        get => null;
        set { if (value.HasValue) CategoryId = value.Value; }
    }
    public long? SubGroupId
    {
        get => null;
        set { if (value.HasValue) CategoryId = value.Value; }
    }
    #endregion
    public ItemType ItemType { get; set; }
    public int UomBase { get; set; }
    public CostingMethod CostingMethod { get; set; }
    public decimal StandardCost { get; set; }
    public decimal DefaultSellingPrice { get; set; }
    public bool ShowInPos { get; set; } = true;
    
    public bool SerialTracking { get; set; }
    public bool LotTracking { get; set; }
    public bool ExpiryTracking { get; set; }
    public bool HasVariants { get; set; }
    public int? ShelfLifeDays { get; set; }
    
    public bool AllowNegativeStock { get; set; }
    public decimal ReorderPoint { get; set; }
    public decimal SafetyStock { get; set; }
    public decimal MinOrderQty { get; set; }
    public int LeadTimeDays { get; set; }
    
    public decimal Weight { get; set; }
    public int? WeightUnit { get; set; }
    
    public string? GlControlAccount { get; set; }
    public string? GlRevenueAccount { get; set; }
    public string? GlCogsAccount { get; set; }
    
    public string? CountryOfOrigin { get; set; }
    public string? HsCode { get; set; }
    public string? Notes { get; set; }

    public string? ImageBase64 { get; set; }
    public int? ColorCode { get; set; }
    
    // Tax Integration
    public long? TaxRateId { get; set; }
    public long? TaxGroupId { get; set; }
    public bool IsTaxExempt { get; set; }
    public string? TaxExemptionReasonCode { get; set; }

    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public InvItemCategory? Category { get; set; }
    public InvItemCategory? MainCategory
    {
        get => Category;
        set => Category = value;
    }
    public InvItemCategory? MainGroup
    {
        get => Category;
        set => Category = value;
    }
    public InvItemCategory? SubCategory
    {
        get => null;
        set { if (value != null) Category = value; }
    }
    public InvItemCategory? SubGroup
    {
        get => null;
        set { if (value != null) Category = value; }
    }
    public ThinkOnErp.Domain.Entities.Accounting.TaxRate? TaxRate { get; set; }
    public ThinkOnErp.Domain.Entities.Accounting.TaxGroup? TaxGroup { get; set; }
    public List<InvItemUomConversion> UomConversions { get; set; } = new();
    public List<InvItemBarcode> Barcodes { get; set; } = new();
    public List<InvStockBalance> StockBalances { get; set; } = new();
    public List<InvLotMaster> Lots { get; set; } = new();
    public List<InvSerialMaster> Serials { get; set; } = new();
    public List<InvBomHeader> AssembledBoms { get; set; } = new();
    public List<InvItemVariant> Variants { get; set; } = new();
    public List<InvItemModifierGroup> ModifierGroups { get; set; } = new();
}
