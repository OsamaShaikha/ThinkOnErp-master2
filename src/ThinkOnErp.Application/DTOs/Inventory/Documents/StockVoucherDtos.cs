using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class CreateStockVoucherDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    /// <summary>
    /// Inbound: 3001 (Opening Stock), 3002 (Inventory Surplus), 3003 (Samples and Gifts).
    /// Outbound: 3011 (Inventory Shortage), 3012 (Scrap and Write-off), 3014 (Internal Consumption).
    /// </summary>
    [Required]
    public int TrxType { get; set; }

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Target warehouse: acts as receiving warehouse for inbound, or issuing warehouse for outbound.
    /// </summary>
    [Required(ErrorMessage = "WarehouseId is required.")]
    public long WarehouseId { get; set; }

    /// <summary>
    /// General ledger expense/adjustment account for scrap, internal use, or inventory adjustment.
    /// </summary>
    public string? GlAccountCode { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Stock voucher must contain at least one line.")]
    public List<StockVoucherLineDto> Lines { get; set; } = new();
}

public sealed class UpdateStockVoucherDto
{
    public DateTime? DocDate { get; set; }
    public long? WarehouseId { get; set; }
    public string? GlAccountCode { get; set; }
    public string? Notes { get; set; }
    public List<StockVoucherLineDto>? Lines { get; set; }
}

public sealed class StockVoucherLineDto
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

    public decimal UnitCost { get; set; }

    public string? GlAccountCode { get; set; }
    public long? BinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}
