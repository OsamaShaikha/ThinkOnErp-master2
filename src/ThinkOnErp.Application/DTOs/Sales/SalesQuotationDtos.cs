using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Sales;

public sealed class CreateSalesQuotationDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    public int TrxType { get; set; } = 4001;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Quotation validity expiry date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    [Required(ErrorMessage = "Customer (PartyId) is required for sales quotations.")]
    public long PartyId { get; set; }

    public string? PartyName { get; set; }

    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Quotation must contain at least one line.")]
    public List<SalesQuotationLineDto> Lines { get; set; } = new();
}

public sealed class UpdateSalesQuotationDto
{
    public DateTime? DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PartyName { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<SalesQuotationLineDto>? Lines { get; set; }
}

public sealed class SalesQuotationLineDto
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
    public decimal UnitPrice { get; set; }

    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string? Notes { get; set; }
}
