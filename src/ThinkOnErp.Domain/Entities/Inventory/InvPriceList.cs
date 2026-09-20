using System;
using System.Collections.Generic;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Inventory;
using ThinkOnErp.Domain.Entities.Pos.Enums;

namespace ThinkOnErp.Domain.Entities.Inventory;

public class InvPriceList
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string PriceListCode { get; set; } = string.Empty;
    public string PriceListNameLocal { get; set; } = string.Empty;
    public string? PriceListNameEn { get; set; }
    public PosOrderType? ApplicableOrderType { get; set; }
    public long? CurrencyId { get; set; }
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    // Navigation
    public SysBranch? Branch { get; set; }
    public ICollection<InvPriceListItem> Items { get; set; } = new List<InvPriceListItem>();
}

public class InvPriceListItem
{
    public long Id { get; set; }
    public long PriceListId { get; set; }
    public long ItemId { get; set; }
    public decimal Price { get; set; }
    public decimal? MinPrice { get; set; }

    // Navigation
    public InvPriceList? PriceList { get; set; }
    public InvItem? Item { get; set; }
}
