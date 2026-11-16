namespace ThinkOnErp.Domain.Entities.Hr;

// First-time configuration only. Changing an established zone requires a dated
// migration workflow so historical local dates cannot be reinterpreted silently.
public sealed class AttendanceTimeSettings
{
    public byte Id { get; set; } = 1;
    [System.ComponentModel.DataAnnotations.Schema.NotMapped]
    public long CompanyId { get; set; }
    public string TimeZoneId { get; set; } = string.Empty;
    public string CreationUser { get; set; } = string.Empty;
    public DateTime CreationDate { get; set; } = DateTime.UtcNow;
}
