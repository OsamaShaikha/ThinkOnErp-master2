using System.Text.Json.Serialization;
using ThinkOnErp.Application.Common.Converters;

namespace ThinkOnErp.Application.DTOs.Inventory.Warehouses;

public class UpdateInvZoneDto
{
    public string? ZoneName { get; set; }

    /// <summary>
    /// نوع منطقة المستودع برقم كود النظام (SysCode CODE_MGR=23):
    /// 1 = تخزين (Storage)
    /// 2 = استلام (Receiving)
    /// 3 = تجهيز / مرحلي (Staging)
    /// 4 = حجر صحي / فحص (Quarantine)
    /// 5 = بضاعة تالفة (Damaged)
    /// 6 = مرتجعات (Returns)
    /// </summary>
    [JsonConverter(typeof(NullableZoneTypeIntConverter))]
    public int? ZoneType { get; set; }
}
