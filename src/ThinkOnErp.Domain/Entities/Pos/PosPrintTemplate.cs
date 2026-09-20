using System;
using ThinkOnErp.Domain.Entities;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Domain.Entities.Pos;

public class PosPrintTemplate
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "Receipt"; // Receipt, KitchenTicket, TaxInvoice, ShelfLabel
    public string RawEscPosPattern { get; set; } = string.Empty; // ESC/POS markup or JSON layout
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    // Navigation
    public SysBranch? Branch { get; set; }
}

public class PosPrinterRouting
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string StationName { get; set; } = string.Empty; // e.g. "KitchenHot", "KitchenCold", "Bar"
    public string PrinterNameOrIp { get; set; } = string.Empty;
    public long? ItemCategoryId { get; set; }
    public long? ItemGroupId
    {
        get => ItemCategoryId;
        set => ItemCategoryId = value;
    }
    public int Copies { get; set; } = 1;
    public bool IsActive { get; set; } = true;

    // Navigation
    public SysBranch? Branch { get; set; }
    public InvItemCategory? ItemCategory { get; set; }
    public InvItemCategory? ItemGroup
    {
        get => ItemCategory;
        set => ItemCategory = value;
    }
}
