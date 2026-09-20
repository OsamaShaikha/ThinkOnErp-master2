using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeaveType
{
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string NameLocal { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public bool IsStatutory { get; set; } = true;
    public decimal MaxDaysPerYear { get; set; } = 14m;
    public bool CarryForwardAllowed { get; set; }
    public decimal CarryForwardCapDays { get; set; }
    public bool RequiresDocumentation { get; set; }
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<LeaveRequest> Requests { get; set; } = new();
    public List<LeaveBalance> Balances { get; set; } = new();
}

public sealed class LeaveRequest
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public DateTime StartDate { get; set; }
    public DateTime EndDate { get; set; }
    public decimal DaysRequested { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING, APPROVED, REJECTED, CANCELLED
    public string? Reason { get; set; }
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? RejectionReason { get; set; }
    public string? AttachmentFileRef { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public LeaveType? LeaveType { get; set; }
}

public sealed class LeaveBalance
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public int YearNo { get; set; }
    public decimal AccruedDays { get; set; }
    public decimal UsedDays { get; set; }
    public decimal CarriedForwardDays { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
    public LeaveType? LeaveType { get; set; }
}

public sealed class LeavePolicy
{
    public long Id { get; set; }
    public string PolicyName { get; set; } = string.Empty;
    public string LeaveTypeCode { get; set; } = string.Empty;
    public string AccrualMethod { get; set; } = "YEARLY"; // YEARLY, MONTHLY, PRO_RATA
    public decimal AccrualRate { get; set; }
    public string ApplicableTo { get; set; } = "ALL"; // ALL, GENDER_F, GENDER_M, CITIZENS
    public int MinServiceMonths { get; set; }
    public int Tier1YearsThreshold { get; set; } = 5;
    public decimal Tier1Days { get; set; } = 14m;
    public decimal Tier2Days { get; set; } = 21m;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public LeaveType? LeaveType { get; set; }
}
