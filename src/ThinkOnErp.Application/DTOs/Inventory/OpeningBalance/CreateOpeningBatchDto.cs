using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.OpeningBalance;

public sealed class CreateOpeningBatchDto
{
    [Required]
    public long BranchId { get; set; }

    [Required]
    [MaxLength(30)]
    public string BatchNo { get; set; } = string.Empty;

    public DateTime BatchDate { get; set; } = DateTime.UtcNow;

    [Required]
    public long FiscalYearId { get; set; }

    public string? Description { get; set; }

    [Required]
    [MinLength(1, ErrorMessage = "Batch must contain at least one item line.")]
    public List<CreateOpeningLineDto> Lines { get; set; } = new();
}

public sealed class CreateOpeningLineDto
{
    [Required]
    public long WarehouseId { get; set; }

    [Required]
    public long ItemId { get; set; }

    public long? BinId { get; set; }

    [Required]
    public string UomCode { get; set; } = string.Empty;

    public decimal UomFactor { get; set; } = 1;

    [Range(0.0001, 99999999)]
    public decimal Quantity { get; set; }

    [Range(0, 99999999)]
    public decimal UnitCost { get; set; }

    public string? LotNumber { get; set; }
    public string? SerialNumber { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
}
