namespace ThinkOnErp.Domain.Entities;

public class SysReportSchedule
{
    public long Id { get; set; }
    public string ReportType { get; set; } = string.Empty;
    public string Frequency { get; set; } = string.Empty;
    public int? DayOfWeek { get; set; }
    public int? DayOfMonth { get; set; }
    public string TimeOfDay { get; set; } = string.Empty;
    public string Recipients { get; set; } = string.Empty;
    public string ExportFormat { get; set; } = string.Empty;
    public string? Parameters { get; set; }
    public bool IsActive { get; set; }
    public long CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? LastGeneratedAt { get; set; }
    public string? LastGenerationStatus { get; set; }
    public string? LastErrorMessage { get; set; }
}
