using System;
using System.ComponentModel.DataAnnotations;

namespace ThinkOnErp.Application.DTOs.Inventory.Items;

public sealed class ItemWarehouseOpeningBalanceDto
{
    [Required]
    public long WarehouseId { get; set; }

    public long? BinId { get; set; }

    [Range(0, double.MaxValue, ErrorMessage = "Quantity must be greater than or equal to 0")]
    public decimal Quantity { get; set; }

    public decimal? UnitCost { get; set; }

    public string? LotNumber { get; set; }

    public string? SerialNumber { get; set; }

    /// <summary>
    /// قائمة الأرقام التسلسلية إذا كان الصنف يخضع للتتبع بالسيريال (List of serial numbers for serial-tracked items)
    /// </summary>
    public List<string>? SerialNumbers { get; set; }

    public DateTime? ExpiryDate { get; set; }

    public string? Notes { get; set; }
}
