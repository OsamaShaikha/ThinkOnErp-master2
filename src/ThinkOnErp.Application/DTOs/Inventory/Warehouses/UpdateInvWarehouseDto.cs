using System.Text.Json.Serialization;
using ThinkOnErp.Application.Common.Converters;

namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class UpdateInvWarehouseDto
{
    public string? WarehouseNameLocal { get; set; }
    public string? WarehouseNameEn { get; set; }

    /// <summary>
    /// نوع المستودع برقم كود النظام (SysCode CODE_MGR=22):
    /// 1 = رئيسي (Main)
    /// 2 = فرعي (Branch)
    /// 3 = عبور/ترانزيت (Transit)
    /// 4 = افتراضي (Virtual)
    /// </summary>
    [JsonConverter(typeof(NullableWarehouseTypeIntConverter))]
    public int? WarehouseType { get; set; }

    public string? Address { get; set; }
    public bool? EnableBinTracking { get; set; }
}
