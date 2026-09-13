using System;

namespace ThinkOnErp.Application.DTOs.Pos;

public class CreatePrintTemplateDto
{
    public long BranchId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "Receipt"; // Receipt, KitchenTicket, TaxInvoice, ShelfLabel
    public string RawEscPosPattern { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
}

public class UpdatePrintTemplateDto
{
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateType { get; set; } = "Receipt";
    public string RawEscPosPattern { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PrintTemplateDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string TemplateCode { get; set; } = string.Empty;
    public string TemplateName { get; set; } = string.Empty;
    public string TemplateType { get; set; } = string.Empty;
    public string RawEscPosPattern { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; }
}

public class CreatePrinterRoutingDto
{
    public long BranchId { get; set; }
    public string StationName { get; set; } = string.Empty; // e.g. KitchenHot, Bar
    public string PrinterNameOrIp { get; set; } = string.Empty;
    public long? ItemGroupId { get; set; }
    public int Copies { get; set; } = 1;
}

public class UpdatePrinterRoutingDto
{
    public string StationName { get; set; } = string.Empty;
    public string PrinterNameOrIp { get; set; } = string.Empty;
    public long? ItemGroupId { get; set; }
    public int Copies { get; set; } = 1;
    public bool IsActive { get; set; } = true;
}

public class PrinterRoutingDto
{
    public long Id { get; set; }
    public long BranchId { get; set; }
    public string StationName { get; set; } = string.Empty;
    public string PrinterNameOrIp { get; set; } = string.Empty;
    public long? ItemGroupId { get; set; }
    public string? ItemGroupName { get; set; }
    public int Copies { get; set; }
    public bool IsActive { get; set; }
}
