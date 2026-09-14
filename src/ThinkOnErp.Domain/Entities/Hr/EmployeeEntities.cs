using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class Employee
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "M"; // M, F
    public string MaritalStatus { get; set; } = "SINGLE"; // SINGLE, MARRIED
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE"; // ACTIVE, SUSPENDED, TERMINATED
    public string? DepartmentCode { get; set; }
    public string? PositionCode { get; set; }
    public long? BranchId { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string? SscNumber { get; set; }
    public int TaxExemptionCount { get; set; }
    public bool IsHighRiskRole { get; set; }
    public string? BankName { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankIban { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<EmployeeDependent> Dependents { get; set; } = new();
    public List<SalaryStructure> SalaryStructures { get; set; } = new();
}

public sealed class EmployeeDependent
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Relationship { get; set; } = string.Empty; // SPOUSE, CHILD, PARENT
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "M";
    public string? NationalId { get; set; }
    public bool IsTaxExemptionClaimed { get; set; } = true;
    public bool IsMedicalCovered { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}

public sealed class SalaryComponent
{
    public string ComponentCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string ComponentType { get; set; } = "ALLOWANCE"; // BASIC, ALLOWANCE, DEDUCTION, EMPLOYER_CONTRIB
    public string CalculationType { get; set; } = "FIXED"; // FIXED, PERCENTAGE, FORMULA
    public decimal? DefaultAmount { get; set; }
    public decimal? DefaultPercent { get; set; }
    public bool IsTaxable { get; set; } = true;
    public bool IsSscApplicable { get; set; } = true;
    public string? GlAccountCode { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class SalaryStructure
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime EffectiveFrom { get; set; }
    public DateTime? EffectiveTo { get; set; }
    public decimal BasicSalary { get; set; }
    public string CurrencyCode { get; set; } = "JOD";
    public string PaymentMethod { get; set; } = "BANK_TRANSFER"; // BANK_TRANSFER, CASH, CHEQUE
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public List<SalaryStructureLine> Lines { get; set; } = new();
}

public sealed class SalaryStructureLine
{
    public long Id { get; set; }
    public long StructureId { get; set; }
    public string ComponentCode { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public decimal? Percent { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;

    public SalaryStructure? Structure { get; set; }
    public SalaryComponent? Component { get; set; }
}
