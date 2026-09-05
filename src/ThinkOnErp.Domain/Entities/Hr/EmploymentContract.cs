using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmploymentContract
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string ContractType { get; set; } = "UNLIMITED"; // LIMITED, UNLIMITED
    public DateTime StartDate { get; set; }
    public DateTime? EndDate { get; set; }
    public string? FileReference { get; set; }
    public string Status { get; set; } = "ACTIVE"; // ACTIVE, EXPIRED, TERMINATED
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
