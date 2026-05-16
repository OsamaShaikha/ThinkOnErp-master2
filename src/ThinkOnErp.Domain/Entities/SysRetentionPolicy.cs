namespace ThinkOnErp.Domain.Entities;

public class SysRetentionPolicy
{
    public long Id { get; set; }
    public string EventCategory { get; set; } = string.Empty;
    public int RetentionDays { get; set; }
    public bool ArchiveEnabled { get; set; }
    public string? Description { get; set; }
    public DateTime? LastModifiedDate { get; set; }
    public long? LastModifiedBy { get; set; }
}
