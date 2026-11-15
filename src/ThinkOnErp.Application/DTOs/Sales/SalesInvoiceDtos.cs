using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Sales;

public sealed class CreateSalesInvoiceDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    /// <summary>
    /// Transaction type: 1001 (Cash Sales), 1002 (Credit Sales), 1003 (Export Sales).
    /// </summary>
    [Required]
    public int TrxType { get; set; } = 1001;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Due date for credit sales (required if TrxType == 1002).
    /// </summary>
    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Customer Identifier.
    /// </summary>
    [Required(ErrorMessage = "Customer (PartyId) is required for sales invoices.")]
    public long PartyId { get; set; }

    public string? PartyName { get; set; }

    /// <summary>
    /// Source warehouse where goods are dispatched.
    /// </summary>
    [Required(ErrorMessage = "Source warehouse (FromWarehouseId) is required for sales invoices.")]
    public long FromWarehouseId { get; set; }

    public int PaymentMethodCode { get; set; } = 1;

    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Base document linkage (e.g., DocType 400 for Quotation conversion).
    /// </summary>
    public int? BaseDocType { get; set; }
    public long? BaseDocId { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Invoice must contain at least one line.")]
    public List<SalesInvoiceLineDto> Lines { get; set; } = new();
}

public sealed class UpdateSalesInvoiceDto
{
    public DateTime? DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PartyName { get; set; }
    public long? FromWarehouseId { get; set; }
    public int? PaymentMethodCode { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<SalesInvoiceLineDto>? Lines { get; set; }
}

public sealed class SalesInvoiceLineDto
{
    [Required]
    public long ItemId { get; set; }

    public string? ItemDescription { get; set; }

    [Required]
    public int UomCode { get; set; }

    public decimal UomFactor { get; set; } = 1;

    [Required]
    [Range(0.0001, 999999999, ErrorMessage = "Quantity must be greater than zero.")]
    public decimal Quantity { get; set; }

    [Required]
    [Range(0, 999999999, ErrorMessage = "UnitPrice cannot be negative.")]
    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }

    public long? FromBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public int? BaseLineNo { get; set; }
    public string? Notes { get; set; }
}
