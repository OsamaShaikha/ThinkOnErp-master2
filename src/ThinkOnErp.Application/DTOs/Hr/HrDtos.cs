using System;
using System.ComponentModel.DataAnnotations;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed record EmployeeTeamMemberDto(string EmployeeCode, string NameLocal, string NameEn, string? DepartmentCode,
    string? PositionCode, long? BranchId, string EmploymentStatus, bool IsActive);

// Employee Management DTOs
public record LinkEmployeeUserDto(long? UserId);
public record EmployeeUserLinkDto(string EmployeeCode, long? UserId);

public record CreateEmployeeDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    DateTime? TerminationDate = null,
    string? TerminationReason = null,
    string? CitizenshipCountryCode = null
);

public record UpdateEmployeeDto(
    string NameLocal,
    string NameEn,
    string NationalId,
    string Nationality,
    string? PassportNumber,
    DateTime DateOfBirth,
    string Gender,
    string MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int TaxExemptionCount,
    bool IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive,
    string? CitizenshipCountryCode = null
);

public record EmployeeSummaryDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string? NationalId,
    string? DepartmentCode,
    string? PositionCode,
    string EmploymentStatus,
    decimal? BasicSalary,
    string? Phone,
    string? Email,
    long? UserId = null,
    long? BranchId = null, EmployeeFieldVisibilityDto? FieldVisibility = null
);

public record EmployeeDetailsDto(
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string? NationalId,
    string? Nationality,
    string? PassportNumber,
    DateTime? DateOfBirth,
    string? Gender,
    string? MaritalStatus,
    string? Email,
    string? Phone,
    DateTime HireDate,
    DateTime? ProbationEndDate,
    DateTime? TerminationDate,
    string? TerminationReason,
    string EmploymentType,
    string EmploymentStatus,
    string? DepartmentCode,
    string? PositionCode,
    long? BranchId,
    string? ManagerEmployeeCode,
    string? SscNumber,
    int? TaxExemptionCount,
    bool? IsHighRiskRole,
    string? BankName,
    string? BankAccountNumber,
    string? BankIban,
    bool IsActive,
    List<DependentDto> Dependents,
    SalaryStructureDetailsDto? ActiveSalaryStructure,
    long? UserId = null,
    EmployeeFieldVisibilityDto? FieldVisibility = null
)
{
    public string? CitizenshipCountryCode { get; init; }
}

public record CreateDependentDto(
    string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered
);

public record DependentDto(
    long Id,
    string EmployeeCode,
    string NameLocal,
    string NameEn,
    string Relationship,
    DateTime DateOfBirth,
    string Gender,
    string? NationalId,
    bool IsTaxExemptionClaimed,
    bool IsMedicalCovered,
    bool IsActive
);

// Salary Structure & Components DTOs
public record AssignSalaryStructureDto(
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    List<SalaryStructureLineDto>? Lines
);

public record SalaryStructureLineDto(
    string ComponentCode,
    decimal? Amount,
    decimal? Percent
);

public record SalaryStructureDetailsDto(
    long Id,
    string EmployeeCode,
    DateTime EffectiveFrom,
    DateTime? EffectiveTo,
    decimal BasicSalary,
    string CurrencyCode,
    string PaymentMethod,
    bool IsActive,
    List<SalaryStructureLineDetailDto> Lines
);

public record SalaryStructureLineDetailDto(
    long Id,
    string ComponentCode,
    string ComponentNameLocal,
    string ComponentNameEn,
    string ComponentType,
    decimal Amount,
    decimal? Percent,
    bool IsActive
);

public record CreateSalaryComponentDto(
    string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType, // BASIC, ALLOWANCE, DEDUCTION, EMPLOYER_CONTRIB
    string CalculationType, // FIXED, PERCENTAGE, FORMULA
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode
);

public record SalaryComponentDto(
    string ComponentCode,
    string NameLocal,
    string NameEn,
    string ComponentType,
    string CalculationType,
    decimal? DefaultAmount,
    decimal? DefaultPercent,
    bool IsTaxable,
    bool IsSscApplicable,
    string? GlAccountCode,
    bool IsActive
);

// Employee Excel Import/Export DTOs
public sealed class EmployeeImportResultDto
{
    public DateTime? PreviewGeneratedAtUtc { get; set; }
    public List<EmployeeImportPreviewDto> PreviewRows { get; set; } = new();
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public int InsertedCount { get; set; }
    public int UpdatedCount { get; set; }
    public bool IsValid => FailureCount == 0;
    public List<EmployeeImportErrorDto> Errors { get; set; } = new();
    public List<string> Warnings { get; set; } = new();
}

public sealed record EmployeeImportErrorDto(
    int RowNumber,
    string? Column,
    string Code,
    string Message
);

public sealed record EmployeeImportPreviewDto(int RowNumber,string EmployeeCode,string Action,bool RequiresUpdateExisting,
    string SalaryChange,DateTime? SalaryEffectiveFrom,DateTime AffectedFrom,
    IReadOnlyList<ThinkOnErp.Domain.Entities.Hr.EmployeeImportPeriodImpact> AffectedPeriods,
    bool HasUnmappedApprovedPayroll,IReadOnlyList<string> BlockingReasons,string? CurrencyCode=null,string? PaymentMethod=null);

public sealed class EmployeeImportRowDto
{
    [global::System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlySet<string>? PresentColumns { get; set; }
    [global::System.Text.Json.Serialization.JsonIgnore]
    public IReadOnlySet<string>? SuppliedFields { get; set; }
    public int RowNumber { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? CitizenshipCountryCode { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime? DateOfBirth { get; set; }
    public string Gender { get; set; } = "M";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
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
    public string? CurrencyCode { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? BasicSalary { get; set; }
    public DateTime? SalaryEffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed class EmployeeExportRowDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string Nationality { get; set; } = string.Empty;
    public string? CitizenshipCountryCode { get; set; }
    public string? PassportNumber { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "M";
    public string MaritalStatus { get; set; } = "SINGLE";
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime HireDate { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME";
    public string EmploymentStatus { get; set; } = "ACTIVE";
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
    public string? CurrencyCode { get; set; }
    public string? PaymentMethod { get; set; }
    public decimal? BasicSalary { get; set; }
    public DateTime? SalaryEffectiveFrom { get; set; }
    public bool IsActive { get; set; } = true;
}

public sealed record EmployeeFieldVisibilityDto(bool Identity,bool Bank,bool Salary,bool EditIdentity,bool EditBank,bool EditSalary);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeWorkDto(string NameLocal,string NameEn,DateTime HireDate,DateTime? ProbationEndDate,
    DateTime? TerminationDate,string? TerminationReason,string EmploymentType,string EmploymentStatus,string? DepartmentCode,
    string? PositionCode,long? BranchId,string? ManagerEmployeeCode,[property: global::System.Text.Json.Serialization.JsonRequired] bool IsActive);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeBankDto(
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankName,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankAccountNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? BankIban);

[global::System.Text.Json.Serialization.JsonUnmappedMemberHandling(global::System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow)]
public sealed record UpdateEmployeeIdentityDto(
    [property: global::System.Text.Json.Serialization.JsonRequired] string NationalId,
    [property: global::System.Text.Json.Serialization.JsonRequired] string Nationality,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? PassportNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] DateTime DateOfBirth,
    [property: global::System.Text.Json.Serialization.JsonRequired] string Gender,
    [property: global::System.Text.Json.Serialization.JsonRequired] string MaritalStatus,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? Email,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? Phone,
    [property: global::System.Text.Json.Serialization.JsonRequired] string? SscNumber,
    [property: global::System.Text.Json.Serialization.JsonRequired] int TaxExemptionCount,
    [property: global::System.Text.Json.Serialization.JsonRequired] bool IsHighRiskRole,
    string? CitizenshipCountryCode = null);
