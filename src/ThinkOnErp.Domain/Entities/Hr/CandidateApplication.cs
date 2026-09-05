using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class CandidateApplication
{
    public long Id { get; set; }
    public string CandidateCode { get; set; } = string.Empty;
    public string RequisitionCode { get; set; } = string.Empty;
    public string Stage { get; set; } = "APPLIED"; // APPLIED, SCREENING, INTERVIEW, OFFER, HIRED, REJECTED
    public decimal? OfferedSalary { get; set; }
    public DateTime? InterviewDate { get; set; }
    public string? Notes { get; set; }
    public string? HiredEmployeeCode { get; set; }
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Candidate? Candidate { get; set; }
    public JobRequisition? Requisition { get; set; }
}
