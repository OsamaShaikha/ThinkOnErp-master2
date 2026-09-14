using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class OvertimeRule
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string DayType { get; set; } = "REGULAR"; // REGULAR, WEEKEND, HOLIDAY
    public decimal Multiplier { get; set; } = 1.25m;
    public int MinimumMinutes { get; set; } = 30;
    public int MaximumMinutes { get; set; } = 240;
    public string HourlyDivisorFormula { get; set; } = "MONTHLY_SALARY / 240";
    public bool RequiresApproval { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class ProrationPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public string Method { get; set; } = "CALENDAR_DAYS"; // CALENDAR_DAYS, FIXED_30, WORKING_DAYS
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class DeductionPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal MaxDeductionPercentage { get; set; } = 33.00m; // Max 33% or 50%
    public decimal MinNetPayGuarantee { get; set; } = 0.00m;
    public bool AutoCapAndCarryForward { get; set; } = true;
    public bool AllowNegativeNetPay { get; set; } = false;
    public bool IsDefault { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class TaxPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal PersonalExemptionSelf { get; set; } = 9000.00m;
    public decimal PersonalExemptionDependent { get; set; } = 1000.00m;
    public decimal NationalContribThreshold { get; set; } = 200000.00m;
    public decimal NationalContribRate { get; set; } = 0.010000m;
    public bool IsSscTaxDeductible { get; set; } = true;
    public string CalculationFrequency { get; set; } = "MONTHLY"; // MONTHLY, ANNUAL
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<TaxBracket> Brackets { get; set; } = new();
}

public sealed class TaxBracket
{
    public long Id { get; set; }
    public long TaxPolicyId { get; set; }
    public int BracketOrder { get; set; }
    public decimal LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal RatePercent { get; set; } // e.g. 5.000000, 10.000000
    public string? Description { get; set; }

    public TaxPolicy? TaxPolicy { get; set; }
}

public sealed class SSCPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal EmployeeContribRate { get; set; } = 0.075000m; // 7.5%
    public decimal EmployerContribRate { get; set; } = 0.142500m; // 14.25%
    public decimal HighRiskSurchargeRate { get; set; } = 0.010000m; // 1.0%
    public decimal MonthlyCeilingCap { get; set; } = 3617.000m;
    public decimal MinimumWageFloor { get; set; } = 260.000m;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
