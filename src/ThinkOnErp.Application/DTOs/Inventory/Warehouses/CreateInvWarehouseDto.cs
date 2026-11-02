using System.Text.Json.Serialization;
using ThinkOnErp.Application.Common.Converters;

namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvWarehouseDto
{
    public long BranchId { get; set; } = 1;
    public long WarehouseCode { get; set; }
    public string WarehouseNameLocal { get; set; } = string.Empty;
    public string WarehouseNameEn { get; set; } = string.Empty;

    /// <summary>
    /// نوع المستودع برقم كود النظام (SysCode CODE_MGR=22):
    /// 1 = رئيسي (Main)
    /// 2 = فرعي (Branch)
    /// 3 = عبور/ترانزيت (Transit)
    /// 4 = افتراضي (Virtual)
    /// </summary>
    [JsonConverter(typeof(WarehouseTypeIntConverter))]
    public int WarehouseType { get; set; } = 1;

    public string? Address { get; set; }
    public bool EnableBinTracking { get; set; }
}
