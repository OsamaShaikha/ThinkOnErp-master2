using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EmployeeDependentDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Relationship { get; set; } = "CHILD";
    public string? NationalId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public bool IsTaxExemptionClaimed { get; set; }
    public bool IsMedicalCovered { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateEmployeeDependentDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Relationship { get; set; } = "CHILD";
    public string? NationalId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public bool IsTaxExemptionClaimed { get; set; } = true;
    public bool IsMedicalCovered { get; set; } = false;
}

public sealed class UpdateEmployeeDependentDto
{
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Relationship { get; set; } = "CHILD";
    public string? NationalId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE";
    public bool IsTaxExemptionClaimed { get; set; } = true;
    public bool IsMedicalCovered { get; set; } = false;
    public bool IsActive { get; set; } = true;
}
