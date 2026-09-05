using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class LeaveType
{
    public string LeaveTypeCode { get; set; } = string.Empty; // ANNUAL, SICK, MATERNITY, PATERNITY, HAJJ, BEREAVEMENT, UNPAID, OTHER
    public string NameAr { get; set; } = string.Empty;
    public string NameEn { get; set; } = string.Empty;
    public bool IsPaid { get; set; } = true;
    public bool IsStatutory { get; set; } = true;
    public bool RequiresDocumentation { get; set; } = false;
    public decimal MaxDaysPerYear { get; set; } = 14m;
    public bool CarryForwardAllowed { get; set; } = false;
    public decimal CarryForwardCapDays { get; set; } = 0m;
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public List<LeavePolicy> Policies { get; set; } = new();
    public List<LeaveBalance> Balances { get; set; } = new();
    public List<LeaveRequest> Requests { get; set; } = new();
}
