using System;
using ThinkOnErp.Domain.Entities.Inventory.Enums;

namespace ThinkOnErp.Application.DTOs.Inventory.Serials;

public class InvSerialListDto
{
    public long Id { get; set; }
    public long ItemId { get; set; }
    public string ItemCode { get; set; } = string.Empty;
    public string ItemNameLocal { get; set; } = string.Empty;
    public string? ItemNameEn { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public long? LotId { get; set; }
    public string? LotNumber { get; set; }
    public SerialStatus Status { get; set; }
    public string StatusName => Status.ToString();
    public long? CurrentWarehouseId { get; set; }
    public string? CurrentWarehouseName { get; set; }
    public long? CurrentBinId { get; set; }
    public string? CurrentBinCode { get; set; }
}

public class InvSerialDetailDto : InvSerialListDto
{
    public DateTime? ExpiryDate { get; set; }
    public DateTime? ManufacturingDate { get; set; }
    public decimal StandardCost { get; set; }
}

public class CreateSerialDto
{
    public long ItemId { get; set; }
    public string SerialNumber { get; set; } = string.Empty;
    public long? LotId { get; set; }
    public SerialStatus Status { get; set; } = SerialStatus.Available;
    public long? CurrentWarehouseId { get; set; }
    public long? CurrentBinId { get; set; }
}

public class UpdateSerialStatusDto
{
    public SerialStatus Status { get; set; }
    public long? CurrentWarehouseId { get; set; }
    public long? CurrentBinId { get; set; }
}
