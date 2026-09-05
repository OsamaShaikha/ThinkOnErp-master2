using System;

namespace ThinkOnErp.Application.DTOs.Hr;

public sealed class EmploymentEventDto
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string EventType { get; set; } = "HIRE";
    public DateTime EffectiveDate { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Reason { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; }
}

public sealed class RecordEmploymentEventDto
{
    public string EventType { get; set; } = "HIRE"; // HIRE, PROBATION_CONFIRM, TRANSFER, PROMOTION, DEMOTION, SUSPENSION, REACTIVATION, TERMINATION, REHIRE, SALARY_ADJUSTMENT
    public DateTime EffectiveDate { get; set; }
    public string? FromValue { get; set; }
    public string? ToValue { get; set; }
    public string? Reason { get; set; }
    public string ApprovedBy { get; set; } = string.Empty;
}
