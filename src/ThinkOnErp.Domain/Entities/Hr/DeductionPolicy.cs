using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class DeductionPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal MaxDeductionPercentage { get; set; } = 50.0m; // Max 50% of disposable salary
    public decimal MinimumNetPayGuarantee { get; set; } = 0.0m;
    public bool AllowNegativeNetPay { get; set; } = false;
    public bool AutoCapAndCarryForward { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
