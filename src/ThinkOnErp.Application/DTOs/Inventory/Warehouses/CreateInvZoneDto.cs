using System.Text.Json.Serialization;
using ThinkOnErp.Application.Common.Converters;

namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class CreateInvZoneDto
{
    public int ZoneCode { get; set; }
    public string ZoneName { get; set; } = string.Empty;

    /// <summary>
    /// نوع منطقة المستودع برقم كود النظام (SysCode CODE_MGR=23):
    /// 1 = تخزين (Storage)
    /// 2 = استلام (Receiving)
    /// 3 = تجهيز / مرحلي (Staging)
    /// 4 = حجر صحي / فحص (Quarantine)
    /// 5 = بضاعة تالفة (Damaged)
    /// 6 = مرتجعات (Returns)
    /// </summary>
    [JsonConverter(typeof(ZoneTypeIntConverter))]
    public int ZoneType { get; set; } = 1;
}
