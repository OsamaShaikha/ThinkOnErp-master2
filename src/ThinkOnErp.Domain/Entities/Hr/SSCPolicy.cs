using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class SSCPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = "JORDAN_SSC_2026";
    public string NameEn { get; set; } = "Jordan Social Security Policy 2026";
    public string NameAr { get; set; } = "سياسة الضمان الاجتماعي الأردنية 2026";
    public decimal EmployeeContributionRate { get; set; } = 0.0750m; // 7.5%
    public decimal EmployerContributionRate { get; set; } = 0.1425m; // 14.25%
    public decimal HighRiskSurchargeRate { get; set; } = 0.0100m; // 1.0%
    public decimal MonthlyCeilingCap { get; set; } = 3349.0m;
    public decimal MinimumWageFloor { get; set; } = 290.0m;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}
