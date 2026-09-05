using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public string Nationality { get; set; } = "Jordanian";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public string MaritalStatus { get; set; } = "SINGLE";
    public DateTime HireDate { get; set; }
    public string? PositionCode { get; set; }
    public string? PositionTitle { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public long? BranchId { get; set; }
    public string? BranchName { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public string? SscNumber { get; set; }
    public bool IsHighRiskRole { get; set; }
    public int TaxExemptionCount { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public string? ManagerName { get; set; }
    public bool IsActive { get; set; }
    public DateTime CreationDate { get; set; }
    public List<EmployeeDependentDto> Dependents { get; set; } = new();
    public List<EmployeeDocumentDto> Documents { get; set; } = new();
}

public sealed class EmployeeSummaryDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string? PositionCode { get; set; }
    public string? PositionTitle { get; set; }
    public string? DepartmentCode { get; set; }
    public string? DepartmentName { get; set; }
    public string EmploymentStatus { get; set; } = "ACTIVE";
}

public sealed class CreateEmployeeDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public string Nationality { get; set; } = "Jordanian";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public string MaritalStatus { get; set; } = "SINGLE";
    public DateTime HireDate { get; set; }
    public string? PositionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
    public string? SscNumber { get; set; }
    public bool IsHighRiskRole { get; set; } = false;
    public int TaxExemptionCount { get; set; } = 0;
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public string? ManagerEmployeeCode { get; set; }
}

public sealed class UpdateEmployeeDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public string Nationality { get; set; } = "Jordanian";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string? PositionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string? SscNumber { get; set; }
    public bool IsHighRiskRole { get; set; } = false;
    public int TaxExemptionCount { get; set; } = 0;
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class ChangeEmployeeStatusDto
{
    public string NewStatus { get; set; } = string.Empty; // PROBATION, ACTIVE, SUSPENDED, ON_LEAVE, TERMINATED
    public DateTime EffectiveDate { get; set; }
    public string? Reason { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
}
