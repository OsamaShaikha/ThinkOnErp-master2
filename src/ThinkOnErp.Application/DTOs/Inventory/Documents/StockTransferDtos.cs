using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.Documents;

public sealed class CreateStockTransferDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    public int DocYear { get; set; }

    public int TrxType { get; set; } = 3501;

    public string? DocNo { get; set; }

    [Required]
    public DateTime DocDate { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Source warehouse where goods depart.
    /// </summary>
    [Required(ErrorMessage = "Source warehouse (FromWarehouseId) is required.")]
    public long FromWarehouseId { get; set; }

    /// <summary>
    /// Destination warehouse where goods arrive.
    /// </summary>
    [Required(ErrorMessage = "Destination warehouse (ToWarehouseId) is required.")]
    public long ToWarehouseId { get; set; }

    public string? Notes { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Stock transfer must contain at least one line.")]
    public List<StockTransferLineDto> Lines { get; set; } = new();
}

public sealed class UpdateStockTransferDto
{
    public DateTime? DocDate { get; set; }
    public long? FromWarehouseId { get; set; }
    public long? ToWarehouseId { get; set; }
    public string? Notes { get; set; }
    public List<StockTransferLineDto>? Lines { get; set; }
}

public sealed class StockTransferLineDto
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

    public long? FromBinId { get; set; }
    public long? ToBinId { get; set; }
    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public string? Notes { get; set; }
}
