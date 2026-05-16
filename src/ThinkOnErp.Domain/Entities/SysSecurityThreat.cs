namespace ThinkOnErp.Domain.Entities;

public class SysSecurityThreat
{
    public long Id { get; set; }
    public string ThreatType { get; set; } = string.Empty;
    public string Severity { get; set; } = string.Empty;
    public string? IpAddress { get; set; }
    public long? UserId { get; set; }
    public long? CompanyId { get; set; }
    public string Description { get; set; } = string.Empty;
    public DateTime DetectionDate { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Metadata { get; set; }
    public long? AcknowledgedBy { get; set; }
    public DateTime? AcknowledgedDate { get; set; }
    public DateTime? ResolvedDate { get; set; }
}
