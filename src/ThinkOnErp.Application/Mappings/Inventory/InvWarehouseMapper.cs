using System.Linq;
using ThinkOnErp.Application.DTOs.Inventory.Warehouses;
using ThinkOnErp.Domain.Entities.Inventory;

namespace ThinkOnErp.Application.Mappings.Inventory;

public static class InvWarehouseMapper
{
    public static InvWarehouseDto ToDto(InvWarehouse entity)
    {
        if (entity == null) return null!;

        return new InvWarehouseDto
        {
            Id = entity.Id,
            WarehouseCode = entity.WarehouseCode,
            WarehouseNameAr = entity.WarehouseNameAr,
            WarehouseNameEn = entity.WarehouseNameEn,
            WarehouseType = entity.WarehouseType.ToString(),
            Address = entity.Address,
            EnableBinTracking = entity.EnableBinTracking,
            IsActive = entity.IsActive,
            Zones = entity.Zones?.Select(z => new InvZoneDto
            {
                Id = z.Id,
                ZoneCode = z.ZoneCode,
                ZoneName = z.ZoneName,
                ZoneType = z.ZoneType.ToString(),
                Bins = z.Bins?.Select(b => new InvBinDto
                {
                    Id = b.Id,
                    BinCode = b.BinCode,
                    MaxWeight = b.MaxWeight,
                    MaxVolume = b.MaxVolume
                }).ToList() ?? new()
            }).ToList() ?? new()
        };
    }
}
