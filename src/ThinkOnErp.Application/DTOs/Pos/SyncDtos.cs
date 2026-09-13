using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Pos;

public class PullCatalogSyncDto
{
    public DateTime ServerTime { get; set; } = DateTime.UtcNow;
    public List<SyncCategoryDto> Categories { get; set; } = new();
    public List<SyncItemDto> Items { get; set; } = new();
    public List<SyncPriceListDto> PriceLists { get; set; } = new();
    public List<SyncPromotionDto> Promotions { get; set; } = new();
    public List<SyncFloorDto> Floors { get; set; } = new();
    public List<PosModifierGroupDto> ModifierGroups { get; set; } = new();
}

public class SyncCategoryDto
{
    public long Id { get; set; }
    public string GroupCode { get; set; } = string.Empty;
    public string GroupNameLocal { get; set; } = string.Empty;
    public string? GroupNameEn { get; set; }
    public long? ParentGroupId { get; set; }
    public int SortOrder { get; set; }
}

public class SyncItemDto
{
    public long Id { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public long GroupId { get; set; }
    public int UomId { get; set; }
    public decimal StandardPrice { get; set; }
    public bool IsScaleItem { get; set; }
    public string? Barcode { get; set; }
    public string? ImageBase64 { get; set; }
}

public class SyncPriceListDto
{
    public long Id { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListName { get; set; } = string.Empty;
    public Dictionary<long, decimal> ItemPrices { get; set; } = new();
}

public class SyncPromotionDto
{
    public long Id { get; set; }
    public string PromotionCode { get; set; } = string.Empty;
    public string PromotionName { get; set; } = string.Empty;
    public int PromotionType { get; set; }
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
}

public class SyncFloorDto
{
    public long Id { get; set; }
    public string FloorName { get; set; } = string.Empty;
    public List<SyncTableDto> Tables { get; set; } = new();
}

public class SyncTableDto
{
    public long Id { get; set; }
    public string TableNumber { get; set; } = string.Empty;
    public int Capacity { get; set; }
    public decimal PositionX { get; set; }
    public decimal PositionY { get; set; }
    public decimal Width { get; set; }
    public decimal Height { get; set; }
    public string Shape { get; set; } = "Square";
}

public class PushOfflineOrdersResultDto
{
    public int TotalProcessed { get; set; }
    public int SuccessCount { get; set; }
    public int ConflictCount { get; set; }
    public List<OrderSyncResultItem> SyncedOrders { get; set; } = new();
}

public class OrderSyncResultItem
{
    public string ClientUuid { get; set; } = string.Empty;
    public long ServerOrderId { get; set; }
    public string OrderNumber { get; set; } = string.Empty;
    public bool HasConflict { get; set; }
    public string? Message { get; set; }
}
