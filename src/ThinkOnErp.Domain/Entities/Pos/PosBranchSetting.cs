using System;
using ThinkOnErp.Domain.Entities;

namespace ThinkOnErp.Domain.Entities.Pos;

/// <summary>
/// إعدادات نقطة البيع الخاصة بالفرع (الحسابات الافتراضية، المستودع الافتراضي، إلخ)
/// </summary>
public class PosBranchSetting
{
    public long Id { get; set; }
    public long BranchId { get; set; }

    /// <summary>
    /// حساب صندوق نقطة البيع الافتراضي للفرع (مثلاً: 111102)
    /// </summary>
    public string? DefaultCashAccount { get; set; }

    /// <summary>
    /// حساب المصروفات النثرية الافتراضي لسندات الصرف للفرع (مثلاً: 525101)
    /// </summary>
    public string? DefaultExpenseAccount { get; set; }

    /// <summary>
    /// حساب الإيرادات والمقبوضات الافتراضي لسندات القبض للفرع (مثلاً: 421101)
    /// </summary>
    public string? DefaultReceiptAccount { get; set; }

    /// <summary>
    /// المستودع الافتراضي لمبيعات الـ POS للفرع
    /// </summary>
    public long? DefaultWarehouseId { get; set; }

    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = "SYSTEM";
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    // Navigation
    public SysBranch? Branch { get; set; }
}
