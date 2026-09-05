using System;

namespace ThinkOnErp.Domain.Entities.Hr;

public sealed class EmployeeDocument
{
    public long Id { get; set; }
    public string EmployeeCode { get; set; } = string.Empty;
    public string DocumentType { get; set; } = "NATIONAL_ID"; // NATIONAL_ID, PASSPORT, CONTRACT, CERTIFICATE, WORK_PERMIT, RESIDENCY, HEALTH_CERTIFICATE, OTHER
    public string? DocumentNumber { get; set; }
    public string FileReference { get; set; } = string.Empty;
    public string? FileName { get; set; }
    public DateTime? IssuedDate { get; set; }
    public DateTime? ExpiryDate { get; set; }
    public string? Notes { get; set; }
    public bool IsActive { get; set; } = true;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
    public string? UpdateUser { get; set; }
    public DateTime? UpdateDate { get; set; }

    public Employee? Employee { get; set; }
}
