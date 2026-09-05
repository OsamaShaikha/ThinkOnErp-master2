using System;
using System.Collections.Generic;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class LeaveTypeDto
{
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsPaid { get; set; }
    public bool IsStatutory { get; set; }
    public bool RequiresDocumentation { get; set; }
    public decimal MaxDaysPerYear { get; set; }
    public bool CarryForwardAllowed { get; set; }
    public decimal CarryForwardCapDays { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateLeaveTypeDto
{
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public bool IsStatutory { get; set; } = false;
    public bool RequiresDocumentation { get; set; } = false;
    public decimal MaxDaysPerYear { get; set; } = 14m;
    public bool CarryForwardAllowed { get; set; } = false;
    public decimal CarryForwardCapDays { get; set; } = 0m;
}

public sealed class LeavePolicyDto
{
    public long Id { get; set; }
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string ApplicableTo { get; set; } = "ALL";
    public string AccrualMethod { get; set; } = "SERVICE_TIERED";
    public decimal AccrualRate { get; set; }
    public int MinServiceMonths { get; set; }
    public int Tier1YearsThreshold { get; set; }
    public decimal Tier1Days { get; set; }
    public decimal Tier2Days { get; set; }
    public bool IsActive { get; set; }
}

public sealed class CreateLeavePolicyDto
{
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string PolicyName { get; set; } = string.Empty;
    public string ApplicableTo { get; set; } = "ALL";
    public string AccrualMethod { get; set; } = "SERVICE_TIERED";
    public decimal AccrualRate { get; set; } = 1.1667m;
    public int MinServiceMonths { get; set; } = 0;
    public int Tier1YearsThreshold { get; set; } = 5;
    public decimal Tier1Days { get; set; } = 14m;
    public decimal Tier2Days { get; set; } = 21m;
}

public sealed class LeaveBalanceDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string LeaveTypeNameEn { get; set; } = string.Empty;
    public int Year { get; set; }
    public decimal AccruedDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal CarriedForwardDays { get; set; }
    public decimal RemainingDays { get; set; }
}

public sealed class LeaveRequestDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EmployeeNameAr { get; set; } = string.Empty;
    public string EmployeeNameEn { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string LeaveTypeNameEn { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string Status { get; set; } = "PENDING";
    public string? Reason { get; set; }
    public string? AttachmentFileReference { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public DateTime CreationDate { get; set; }
}

public sealed class SubmitLeaveRequestDto
{
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string? Reason { get; set; }
    public string? AttachmentFileReference { get; set; }
}

public sealed class RunAccrualResultDto
{
    public int ProcessedEmployees { get; set; }
    public int UpdatedBalances { get; set; }
    public int Year { get; set; }
    public string Message { get; set; } = string.Empty;
}
