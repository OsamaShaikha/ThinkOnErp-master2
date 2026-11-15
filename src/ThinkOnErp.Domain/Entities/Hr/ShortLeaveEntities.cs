using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public static class ShortLeaveTypes
{
    public const string LateArrival = "LATE_ARRIVAL";
    public const string EarlyDeparture = "EARLY_DEPARTURE";
    public const string MiddayExit = "MIDDAY_EXIT";

    public static readonly IReadOnlySet<string> All = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
    {
        LateArrival, EarlyDeparture, MiddayExit
    };
}

public static class ShortLeaveStatus
{
    public const string Draft = "DRAFT";
    public const string PendingApproval = "PENDING_APPROVAL";
    public const string Approved = "APPROVED";
    public const string Rejected = "REJECTED";
    public const string Cancelled = "CANCELLED";
}

public static class ShortLeaveCompensationTypes
{
    public const string Paid = "PAID";
    public const string DeductSalary = "DEDUCT_SALARY";
    public const string DeductLeaveBalance = "DEDUCT_LEAVE_BALANCE";
}

public sealed class ShortLeavePolicy
{
    public long Id { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long CompanyId { get; set; }
    public int MonthlyPaidMinutesQuota { get; set; } = 120; // 2 hours default
    public int MaxMinutesPerRequest { get; set; } = 120;
    public bool AllowMiddayExit { get; set; } = true;
    public string ExcessAction { get; set; } = ShortLeaveCompensationTypes.DeductSalary; // DEDUCT_SALARY, DEDUCT_LEAVE_BALANCE, REJECT
    public string LeaveTypeCodeForDeduction { get; set; } = "ANNUAL";
    public decimal StandardWorkdayHours { get; set; } = 8.0m;
    public bool IsActive { get; set; } = true;

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }
}

public sealed class ShortLeaveRequest
{
    public long Id { get; set; }
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long CompanyId { get; set; }
    public long? BranchId { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;

    public string PermissionType { get; set; } = ShortLeaveTypes.LateArrival;
    public DateTime PermissionDate { get; set; }
    public TimeSpan StartTime { get; set; }
    public TimeSpan EndTime { get; set; }
    public int DurationMinutes { get; set; }
    public string Reason { get; set; } = string.Empty;

    public string Status { get; set; } = ShortLeaveStatus.PendingApproval;
    public string CompensationType { get; set; } = ShortLeaveCompensationTypes.Paid;

    public decimal DeductedSalaryAmount { get; set; }
    public decimal ConvertedLeaveDays { get; set; }

    public string? ApprovedBy { get; set; }
    public DateTime? ApprovedAtUtc { get; set; }

    public string? RejectedBy { get; set; }
    public DateTime? RejectedAtUtc { get; set; }
    public string? RejectionReason { get; set; }

    public string? CancelledBy { get; set; }
    public DateTime? CancelledAtUtc { get; set; }
    public string? CancellationReason { get; set; }

    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}

