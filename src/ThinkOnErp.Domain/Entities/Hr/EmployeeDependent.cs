using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeDependent
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Relationship { get; set; } = "CHILD"; // SPOUSE, CHILD, PARENT, OTHER
    public string? NationalId { get; set; }
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE"; // MALE, FEMALE
    public bool IsTaxExemptionClaimed { get; set; } = true;
    public bool IsMedicalCovered { get; set; } = false;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
