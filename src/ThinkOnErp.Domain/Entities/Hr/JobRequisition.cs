using System;
using System.Collections.Generic;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class JobRequisition
{
    public string RequisitionCode { get; set; } = string.Empty;
    public string PositionCode { get; set; } = string.Empty;
    public string DepartmentCode { get; set; } = string.Empty;
    public long? BranchId { get; set; }
    public int Headcount { get; set; } = 1;
    public DateTime? TargetHireDate { get; set; }
    public string Status { get; set; } = "DRAFT"; // DRAFT, APPROVED, OPEN, FILLED, CANCELLED
    public string RequestedBy { get; set; } = string.Empty;
    public string? ApprovedBy { get; set; }
    public DateTime? ApprovalDate { get; set; }
    public string? JobDescription { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Position? Position { get; set; }
    public Department? Department { get; set; }
    public List<CandidateApplication> Applications { get; set; } = new();
}
