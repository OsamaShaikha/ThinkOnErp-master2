using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class SalaryComponentDto
{
    public string ComponentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING"; // EARNING, DEDUCTION
    public bool IsTaxable { get; set; }
    public bool IsSscApplicable { get; set; }
    public string CalculationType { get; set; } = "FIXED_AMOUNT";
    public decimal? DefaultAmount { get; set; }
    public decimal? DefaultPercent { get; set; }
    public string? GlAccountCode { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateSalaryComponentDto
{
    public string ComponentCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING";
    public bool IsTaxable { get; set; } = true;
    public bool IsSscApplicable { get; set; } = true;
    public string CalculationType { get; set; } = "FIXED_AMOUNT";
    public decimal? DefaultAmount { get; set; }
    public decimal? DefaultPercent { get; set; }
    public string? GlAccountCode { get; set; }
}

public sealed class EmployeeSalaryStructureDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public decimal TotalAllowances { get; set; }
    public decimal TotalGrossSalary { get; set; }
    public bool IsActive { get; set; }
    public List<EmployeeSalaryStructureLineDto> Lines { get; set; } = new();
}

public sealed class EmployeeSalaryStructureLineDto
{
    public long Id { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public string ComponentNameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "EARNING";
    public bool IsTaxable { get; set; }
    public bool IsSscApplicable { get; set; }
    public decimal Amount { get; set; }
    public decimal? Percent { get; set; }
}

public sealed class SetEmployeeSalaryStructureDto
{
    public DateTime EffectiveFrom { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public string PaymentMethod { get; set; } = "BANK_TRANSFER";
    public List<SalaryStructureLineInputDto> Lines { get; set; } = new();
}

public sealed class SalaryStructureLineInputDto
{
    public string ComponentCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Percent { get; set; }
}

public sealed class SalaryRevisionDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime EffectiveDate { get; set; }
    public decimal OldBasicSalary { get; set; }
    public decimal NewBasicSalary { get; set; }
    public decimal OldGrossSalary { get; set; }
    public decimal NewGrossSalary { get; set; }
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public sealed class CreateSalaryRevisionDto
{
    public DateTime EffectiveDate { get; set; }
    public decimal NewBasicSalary { get; set; }
    public List<SalaryStructureLineInputDto> NewAllowanceLines { get; set; } = new();
    public string Reason { get; set; } = string.Empty;
    public string ApprovedBy { get; set; } = string.Empty;
}

public sealed class EmploymentContractDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string ContractType { get; set; } = "UNLIMITED";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? FileReference { get; set; }
    public string Status { get; set; } = "ACTIVE";
    public string? Notes { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateEmploymentContractDto
{
    public string ContractType { get; set; } = "UNLIMITED";
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? FileReference { get; set; }
    public string? Notes { get; set; }
}
