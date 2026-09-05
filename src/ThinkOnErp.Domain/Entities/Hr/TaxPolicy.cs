using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class TaxPolicy
{
    public long Id { get; set; }
    public long CompanyId { get; set; }
    public string Code { get; set; } = "JORDAN_ISTD_2026";
    public string NameEn { get; set; } = "Jordan Income Tax Policy 2026";
    public string NameAr { get; set; } = "سياسة ضريبة الدخل الأردنية 2026";
    public string CalculationFrequency { get; set; } = "ANNUALIZED_MONTHLY"; // ANNUALIZED_MONTHLY, YTD_CUMULATIVE, FLAT_MONTHLY
    public decimal PersonalExemptionSelf { get; set; } = 9000.0m;
    public decimal PersonalExemptionDependent { get; set; } = 9000.0m;
    public decimal NationalContributionThreshold { get; set; } = 200000.0m;
    public decimal NationalContributionRate { get; set; } = 0.010m; // 1%
    public bool IsSscTaxDeductible { get; set; } = true;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public ICollection<TaxBracket> Brackets { get; set; } = new List<TaxBracket>();
}
