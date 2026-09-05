using System;

namespace ThinkOnErp.Domain.Entities.Inventory;

/// <summary>
/// جدول الأرقام التسلسلية للمستندات والفواتير
/// Maintains serial numbers per Branch, Year, Month, and Document Type.
/// </summary>
public sealed class TrxDocumentSerial
{
    public long BranchId { get; set; }
    public int DocYear { get; set; }
    public int DocMonth { get; set; }
    public int DocType { get; set; }
    public long LastSerialNo { get; set; }
    public DateTime? UpdateDate { get; set; }
}
