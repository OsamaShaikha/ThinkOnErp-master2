using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Purchasing;

public sealed class CreatePurchasingBillDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    /// <summary>
    /// Transaction type: 2001 (Local Purchase), 2003 (Import / Foreign Purchase).
    /// </summary>
    [Required]
    public int TrxType { get; set; } = 2001;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    public DateTime? DueDate { get; set; }

    /// <summary>
    /// Vendor Identifier.
    /// </summary>
    [Required(ErrorMessage = "Vendor (PartyId) is required for purchasing bills.")]
    public long PartyId { get; set; }

    public string? PartyName { get; set; }

    /// <summary>
    /// Receiving warehouse.
    /// </summary>
    [Required(ErrorMessage = "Receiving warehouse (ToWarehouseId) is required for purchasing bills.")]
    public long ToWarehouseId { get; set; }

    [MaxLength(3)]
    public string CurrencyCode { get; set; } = "USD";

    [Range(0.000001, 999999)]
    public decimal ExchangeRate { get; set; } = 1;

    public int PaymentMethodCode { get; set; } = 1;

    public decimal DiscountAmount { get; set; }

    /// <summary>
    /// Base document linkage (e.g., DocType 450 for Purchase Order conversion).
    /// </summary>
    public int? BaseDocType { get; set; }
    public long? BaseDocId { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Bill must contain at least one line.")]
    public List<PurchasingBillLineDto> Lines { get; set; } = new();
}

public sealed class UpdatePurchasingBillDto
{
    public DateTime? DocDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? PartyName { get; set; }
    public long? ToWarehouseId { get; set; }
    public string? CurrencyCode { get; set; }
    public decimal? ExchangeRate { get; set; }
    public int? PaymentMethodCode { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<PurchasingBillLineDto>? Lines { get; set; }
}

public sealed class PurchasingBillLineDto
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

    /// <summary>
    /// Purchase unit cost.
    /// </summary>
    [Required]
    [Range(0, 999999999, ErrorMessage = "UnitCost cannot be negative.")]
    public decimal UnitCost { get; set; }

    public decimal DiscountPercent { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal TaxRate { get; set; }

    public long? ToBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public int? BaseLineNo { get; set; }
    public string? Notes { get; set; }
}
