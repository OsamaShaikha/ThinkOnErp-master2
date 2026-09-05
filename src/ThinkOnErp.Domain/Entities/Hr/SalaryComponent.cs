using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class SalaryComponent
{
    public string ComponentCode { get; set; } = string.Empty; // BASIC, HOUSING, TRANSPORT, MOBILE, FAMILY_ALLOWANCE, OVERTIME, BONUS, SSC_EMPLOYEE, INCOME_TAX, LOAN_DEDUCTION, OTHER_DEDUCTION
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION
    public bool IsTaxable { get; set; } = true;
    public bool IsSscApplicable { get; set; } = true;
    public string CalculationType { get; set; } = "FIXED_AMOUNT"; // FIXED_AMOUNT, PERCENT_OF_BASIC, FORMULA
    public decimal? DefaultAmount { get; set; }
    public decimal? DefaultPercent { get; set; }
    public string? GlAccountCode { get; set; } // Specific GL account mapping (optional override)
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<EmployeeSalaryStructureLine> StructureLines { get; set; } = new();
}
