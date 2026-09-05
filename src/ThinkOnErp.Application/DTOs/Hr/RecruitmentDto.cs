using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class JobRequisitionDto
{
    public string RequisitionCode { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string PositionTitleEn { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public string DepartmentNameEn { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public int Headcount { get; set; }
    public DateTime? TargetHireDate { get; set; }
    public string Status { get; set; } = "DRAFT";
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? JobDescription { get; set; }
    public int CandidateCount { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class CreateJobRequisitionDto
{
    public string RequisitionCode { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public int Headcount { get; set; } = 1;
    public DateTime? TargetHireDate { get; set; }
    public string? JobDescription { get; set; }
}

public sealed class CandidateDto
{
    public string CandidateCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public string? ResumeFileReference { get; set; }
    public string Source { get; set; } = "DIRECT";
    public DateTime CreationDate { get; set; }
    public List<CandidateApplicationDto> Applications { get; set; } = new();
}

public sealed class CreateCandidateDto
{
    public string CandidateCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? NationalId { get; set; }
    public string? ResumeFileReference { get; set; }
    public string Source { get; set; } = "DIRECT";
    public string? ApplyRequisitionCode { get; set; }
}

public sealed class CandidateApplicationDto
{
    public long Id { get; set; }
    public string CandidateCode { get; set; } = string.Empty;
    public string CandidateNameEn { get; set; } = string.Empty;
    public string RequisitionCode { get; set; } = string.Empty;
    public string Stage { get; set; } = "APPLIED";
    public decimal? OfferedSalary { get; set; }
    public DateTime? InterviewDate { get; set; }
    public string? Notes { get; set; }
    public string? HiredEmployeeCode { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class UpdateApplicationStageDto
{
    public string Stage { get; set; } = "SCREENING"; // APPLIED, SCREENING, INTERVIEW, OFFER, HIRED, REJECTED
    public decimal? OfferedSalary { get; set; }
    public DateTime? InterviewDate { get; set; }
    public string? Notes { get; set; }
}

public sealed class HireCandidateDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public DateTime HireDate { get; set; }
    public decimal BasicSalary { get; set; }
    public string? NationalId { get; set; }
    public string? SscNumber { get; set; }
    public string? BankAccountNumber { get; set; }
    public string? BankName { get; set; }
    public string? BankIban { get; set; }
    public string? Email { get; set; }
    public string? Phone { get; set; }
}

public sealed class OnboardingTaskDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public bool IsCompleted { get; set; }
    public DateTime? CompletedDate { get; set; }
    public string? CompletedBy { get; set; }
    public string? Notes { get; set; }
}

public sealed class CreateOnboardingTaskDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string TaskName { get; set; } = string.Empty;
    public string? AssignedTo { get; set; }
    public DateTime? DueDate { get; set; }
    public string? Notes { get; set; }
}
