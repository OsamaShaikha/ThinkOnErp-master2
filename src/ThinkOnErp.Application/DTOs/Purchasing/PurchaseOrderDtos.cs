using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Purchasing;

public sealed class CreatePurchaseOrderDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    public int TrxType { get; set; } = 4004;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Expected delivery date.
    /// </summary>
    public DateTime? DueDate { get; set; }

    [Required(ErrorMessage = "Vendor (PartyId) is required for purchase orders.")]
    public long PartyId { get; set; }

    public string? PartyName { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Purchase order must contain at least one line.")]
    public List<PurchaseOrderLineDto> Lines { get; set; } = new();
}

public sealed class UpdatePurchaseOrderDto
{
    public DateTime? DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PartyName { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<PurchaseOrderLineDto>? Lines { get; set; }
}

public sealed class PurchaseOrderLineDto
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
    public decimal UnitCost { get; set; }

    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }
    public string? Notes { get; set; }
}
