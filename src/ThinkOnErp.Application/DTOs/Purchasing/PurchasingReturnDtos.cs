using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Purchasing;

public sealed class CreatePurchasingReturnDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    public int TrxType { get; set; } = 2501;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Mandatory reference to original purchasing bill.
    /// </summary>
    [Required(ErrorMessage = "Original Purchasing Bill ID (BaseDocId) is required for purchase returns.")]
    public long BaseDocId { get; set; }

    [Required(ErrorMessage = "Vendor (PartyId) is required for purchase returns.")]
    public long PartyId { get; set; }

    public string? PartyName { get; set; }

    /// <summary>
    /// Warehouse from which returned items are deducted.
    /// </summary>
    [Required(ErrorMessage = "Source warehouse (FromWarehouseId) is required for purchase returns.")]
    public long FromWarehouseId { get; set; }

    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Purchase return must contain at least one line.")]
    public List<PurchasingReturnLineDto> Lines { get; set; } = new();
}

public sealed class UpdatePurchasingReturnDto
{
    public DateTime? DocDate { get; set; }
    public long? FromWarehouseId { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<PurchasingReturnLineDto>? Lines { get; set; }
}

public sealed class PurchasingReturnLineDto
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

    /// <summary>
    /// Line number in the original purchasing bill.
    /// </summary>
    public int? BaseLineNo { get; set; }

    public long? FromBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
}
