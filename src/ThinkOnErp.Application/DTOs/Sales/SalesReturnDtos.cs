using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Sales;

public sealed class CreateSalesReturnDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    /// <summary>
    /// Transaction type: 1501 (Restock / Goods returned into inventory), 1502 (Scrap / Damaged returned).
    /// </summary>
    [Required]
    public int TrxType { get; set; } = 1501;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Mandatory reference to original sales invoice.
    /// </summary>
    [Required(ErrorMessage = "Original Sales Invoice ID (BaseDocId) is required for sales returns.")]
    public long BaseDocId { get; set; }

    public long? PartyId { get; set; }
    public string? PartyName { get; set; }

    /// <summary>
    /// Warehouse receiving the returned stock. Mandatory for 1501 (Restock); optional for 1502 (Scrap).
    /// </summary>
    public long? ToWarehouseId { get; set; }

    public decimal DiscountAmount { get; set; }
    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Sales return must contain at least one line.")]
    public List<SalesReturnLineDto> Lines { get; set; } = new();
}

public sealed class UpdateSalesReturnDto
{
    public DateTime? DocDate { get; set; }
    public long? ToWarehouseId { get; set; }
    public decimal? DiscountAmount { get; set; }
    public string? Notes { get; set; }
    public List<SalesReturnLineDto>? Lines { get; set; }
}

public sealed class SalesReturnLineDto
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

    /// <summary>
    /// Line number in the original invoice.
    /// </summary>
    public int? BaseLineNo { get; set; }

    public long? ToBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
}
