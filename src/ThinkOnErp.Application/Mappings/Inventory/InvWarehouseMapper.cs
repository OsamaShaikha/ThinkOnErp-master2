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
            WarehouseNameLocal = entity.WarehouseNameLocal,
            WarehouseNameEn = entity.WarehouseNameEn,
            WarehouseType = (int)entity.WarehouseType,
            WarehouseTypeName = ResolveWarehouseTypeName((int)entity.WarehouseType),
            Address = entity.Address,
            EnableBinTracking = entity.EnableBinTracking,
            IsActive = entity.IsActive,
            Zones = entity.Zones?.Select(z => new InvZoneDto
            {
                Id = z.Id,
                ZoneCode = z.ZoneCode,
                ZoneName = z.ZoneName,
                ZoneType = (int)z.ZoneType,
                ZoneTypeName = ResolveZoneTypeName((int)z.ZoneType),
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

    public static string ResolveWarehouseTypeName(int type, int lang = 1)
    {
        var isEn = lang == 2;
        return type switch
        {
            1 => isEn ? "Main Warehouse" : "مستودع رئيسي",
            2 => isEn ? "Branch Warehouse" : "مستودع فرعي",
            3 => isEn ? "Transit Warehouse" : "مستودع ترانزيت / عبور",
            4 => isEn ? "Virtual Warehouse" : "مستودع افتراضي",
            _ => isEn ? "Unknown" : "غير محدد"
        };
    }

    public static string ResolveZoneTypeName(int type, int lang = 1)
    {
        var isEn = lang == 2;
        return type switch
        {
            1 => isEn ? "Storage Zone" : "منطقة تخزين",
            2 => isEn ? "Receiving Zone" : "منطقة استلام",
            3 => isEn ? "Staging Zone" : "منطقة تجهيز / شحن",
            4 => isEn ? "Quarantine Zone" : "منطقة حجر صحي / فحص",
            5 => isEn ? "Damaged Goods Zone" : "منطقة بضاعة تالفة",
            6 => isEn ? "Returns Zone" : "منطقة مرتجعات",
            _ => isEn ? "Unknown" : "غير محدد"
        };
    }
}
