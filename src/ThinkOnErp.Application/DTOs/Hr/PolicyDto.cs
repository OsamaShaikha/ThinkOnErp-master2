using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class OvertimeRuleDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string DayType { get; set; } = "NORMAL";
    public decimal Multiplier { get; set; }
    public int MinimumMinutes { get; set; }
    public int MaximumMinutes { get; set; }
    public string HourlyDivisorFormula { get; set; } = "FIXED_240";
    public bool RequiresApproval { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateOvertimeRuleDto
{
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string DayType { get; set; } = "NORMAL";
    public decimal Multiplier { get; set; } = 1.25m;
    public int MinimumMinutes { get; set; } = 30;
    public int MaximumMinutes { get; set; } = 480;
    public string HourlyDivisorFormula { get; set; } = "FIXED_240";
    public bool RequiresApproval { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class AttendancePolicyDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int GracePeriodMinutes { get; set; }
    public int EarlyLeaveToleranceMinutes { get; set; }
    public int MinimumMinutesForOvertime { get; set; }
    public bool AutoDeductLateArrival { get; set; }
    public string MissingPunchHandling { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateAttendancePolicyDto
{
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public int GracePeriodMinutes { get; set; } = 15;
    public int EarlyLeaveToleranceMinutes { get; set; } = 0;
    public int MinimumMinutesForOvertime { get; set; } = 30;
    public bool AutoDeductLateArrival { get; set; } = true;
    public string MissingPunchHandling { get; set; } = "FLAG_FOR_CORRECTION";
    public bool IsDefault { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class ProrationPolicyDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Method { get; set; } = string.Empty;
    public bool IsDefault { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateProrationPolicyDto
{
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string Method { get; set; } = "FIXED_30";
    public bool IsDefault { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class DeductionPolicyDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal MaxDeductionPercentage { get; set; }
    public decimal MinimumNetPayGuarantee { get; set; }
    public bool AllowNegativeNetPay { get; set; }
    public bool AutoCapAndCarryForward { get; set; }
    public bool IsDefault { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateDeductionPolicyDto
{
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal MaxDeductionPercentage { get; set; } = 50.0m;
    public decimal MinimumNetPayGuarantee { get; set; } = 0.0m;
    public bool AllowNegativeNetPay { get; set; } = false;
    public bool AutoCapAndCarryForward { get; set; } = true;
    public bool IsDefault { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
}

public sealed class TaxPolicyDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string CalculationFrequency { get; set; } = string.Empty;
    public decimal PersonalExemptionSelf { get; set; }
    public decimal PersonalExemptionDependent { get; set; }
    public decimal NationalContributionThreshold { get; set; }
    public decimal NationalContributionRate { get; set; }
    public bool IsSscTaxDeductible { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
    public List<TaxBracketDto> Brackets { get; set; } = new();
}

public sealed class TaxBracketDto
{
    public long Id { get; set; }
    public long TaxPolicyId { get; set; }
    public int BracketOrder { get; set; }
    public decimal LowerLimit { get; set; }
    public decimal? UpperLimit { get; set; }
    public decimal RatePercent { get; set; }
    public string? Description { get; set; }
}

public sealed class SSCPolicyDto
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public decimal EmployeeContributionRate { get; set; }
    public decimal EmployerContributionRate { get; set; }
    public decimal HighRiskSurchargeRate { get; set; }
    public decimal MonthlyCeilingCap { get; set; }
    public decimal MinimumWageFloor { get; set; }
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; }
}
