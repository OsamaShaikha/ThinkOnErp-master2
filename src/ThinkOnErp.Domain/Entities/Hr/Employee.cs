using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class Employee
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string NationalId { get; set; } = string.Empty;
    public string? PassportNumber { get; set; }
    public string Nationality { get; set; } = "Jordanian";
    public DateTime DateOfBirth { get; set; }
    public string Gender { get; set; } = "MALE"; // MALE, FEMALE
    public string MaritalStatus { get; set; } = "SINGLE"; // SINGLE, MARRIED, DIVORCED, WIDOWED
    public DateTime HireDate { get; set; }
    public string? PositionCode { get; set; }
    public string? DepartmentCode { get; set; }
    public long? BranchId { get; set; }
    public string EmploymentType { get; set; } = "FULL_TIME"; // FULL_TIME, PART_TIME, CONTRACT, DAILY_WAGE
    public string EmploymentStatus { get; set; } = "ACTIVE"; // PROBATION, ACTIVE, SUSPENDED, ON_LEAVE, TERMINATED
    public string? SscNumber { get; set; } // Social Security Corporation #
    public bool IsHighRiskRole { get; set; } = false; // +1% SSC surcharge
    public int TaxExemptionCount { get; set; } = 0; // Number of eligible dependents
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
    public DateTime? ProbationEndDate { get; set; }
    public DateTime? TerminationDate { get; set; }
    public string? TerminationReason { get; set; }
    public string? ManagerEmployeeCode { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Position? Position { get; set; }
    public Department? Department { get; set; }
    public SysBranch? Branch { get; set; }
    public Employee? Manager { get; set; }
    public List<Employee> DirectReports { get; set; } = new();
    public List<EmployeeDependent> Dependents { get; set; } = new();
    public List<EmployeeDocument> Documents { get; set; } = new();
    public List<EmploymentEvent> EmploymentEvents { get; set; } = new();
}
