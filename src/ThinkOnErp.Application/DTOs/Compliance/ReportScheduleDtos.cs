namespace ThinkOnErp.Application.DTOs.Compliance;

/// <summary>
/// DTO for report schedule configuration
/// </summary>
public class ReportScheduleDto
{
    /// <summary>
    /// Unique identifier for the schedule
    /// </summary>
    public long Id { get; set; }
    
    /// <summary>
    /// Type of report to generate
    /// </summary>
    public string ReportType { get; set; } = null!;
    
    /// <summary>
    /// Schedule frequency (Daily, Weekly, Monthly)
    /// </summary>
    public string Frequency { get; set; } = null!;
    
    /// <summary>
    /// Day of week for weekly reports (1=Monday, 7=Sunday)
    /// </summary>
    public int? DayOfWeek { get; set; }
    
    /// <summary>
    /// Day of month for monthly reports (1-31)
    /// </summary>
    public int? DayOfMonth { get; set; }
    
    /// <summary>
    /// Time of day to generate the report (HH:mm format)
    /// </summary>
    public string TimeOfDay { get; set; } = "02:00";
    
    /// <summary>
    /// Email addresses to send the report to (comma-separated)
    /// </summary>
    public string Recipients { get; set; } = null!;
    
    /// <summary>
    /// Export format for the report (PDF, CSV, JSON)
    /// </summary>
    public string ExportFormat { get; set; } = null!;
    
    /// <summary>
    /// Additional parameters for the report (JSON format)
    /// </summary>
    public string? Parameters { get; set; }
    
    /// <summary>
    /// Whether the schedule is active
    /// </summary>
    public bool IsActive { get; set; }
    
    /// <summary>
    /// User ID who created the schedule
    /// </summary>
    public long CreatedByUserId { get; set; }
    
    /// <summary>
    /// When the schedule was created
    /// </summary>
    public DateTime CreatedAt { get; set; }
    
    /// <summary>
    /// When the report was last generated
    /// </summary>
    public DateTime? LastGeneratedAt { get; set; }
}

/// <summary>
/// Request DTO for creating a new report schedule
/// </summary>
public class CreateReportScheduleDto
{
    /// <summary>
    /// Type of report to generate
    /// </summary>
    public string ReportType { get; set; } = null!;
    
    /// <summary>
    /// Schedule frequency (Daily, Weekly, Monthly)
    /// </summary>
    public string Frequency { get; set; } = null!;
    
    /// <summary>
    /// Day of week for weekly reports (1=Monday, 7=Sunday)
    /// </summary>
    public int? DayOfWeek { get; set; }
    
    /// <summary>
    /// Day of month for monthly reports (1-31)
    /// </summary>
    public int? DayOfMonth { get; set; }
    
    /// <summary>
    /// Time of day to generate the report (HH:mm format)
    /// </summary>
    public string TimeOfDay { get; set; } = "02:00";
    
    /// <summary>
    /// Email addresses to send the report to (comma-separated)
    /// </summary>
    public string Recipients { get; set; } = null!;
    
    /// <summary>
    /// Export format for the report (PDF, CSV, JSON)
    /// </summary>
    public string ExportFormat { get; set; } = null!;
    
    /// <summary>
    /// Additional parameters for the report (JSON format)
    /// </summary>
    public string? Parameters { get; set; }
}

/// <summary>
/// Request DTO for updating an existing report schedule
/// </summary>
public class UpdateReportScheduleDto
{
    /// <summary>
    /// Schedule frequency (Daily, Weekly, Monthly)
    /// </summary>
    public string? Frequency { get; set; }
    
    /// <summary>
    /// Day of week for weekly reports (1=Monday, 7=Sunday)
    /// </summary>
    public int? DayOfWeek { get; set; }
    
    /// <summary>
    /// Day of month for monthly reports (1-31)
    /// </summary>
    public int? DayOfMonth { get; set; }
    
    /// <summary>
    /// Time of day to generate the report (HH:mm format)
    /// </summary>
    public string? TimeOfDay { get; set; }
    
    /// <summary>
    /// Email addresses to send the report to (comma-separated)
    /// </summary>
    public string? Recipients { get; set; }
    
    /// <summary>
    /// Export format for the report (PDF, CSV, JSON)
    /// </summary>
    public string? ExportFormat { get; set; }
    
    /// <summary>
    /// Additional parameters for the report (JSON format)
    /// </summary>
    public string? Parameters { get; set; }
    
    /// <summary>
    /// Whether the schedule is active
    /// </summary>
    public bool? IsActive { get; set; }
}

/// <summary>
/// Request DTO for exporting a report
/// </summary>
public class ReportExportRequestDto
{
    /// <summary>
    /// Report ID to export
    /// </summary>
    public string ReportId { get; set; } = null!;
    
    /// <summary>
    /// Export format (PDF, CSV, JSON)
    /// </summary>
    public string ExportFormat { get; set; } = null!;
}

/// <summary>
/// DTO for report metadata
/// </summary>
public class ReportMetadataDto
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = null!;
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = null!;
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Username who generated the report
    /// </summary>
    public string? GeneratedByUsername { get; set; }
    
    /// <summary>
    /// Start date of the report period (if applicable)
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (if applicable)
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// Size of the report in bytes
    /// </summary>
    public long? SizeBytes { get; set; }
    
    /// <summary>
    /// Number of records in the report
    /// </summary>
    public int? RecordCount { get; set; }
}

/// <summary>
/// DTO for a report section (used for structured reports)
/// </summary>
public class ReportSectionDto
{
    /// <summary>
    /// Section title
    /// </summary>
    public string Title { get; set; } = null!;
    
    /// <summary>
    /// Section description
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// Section order/sequence
    /// </summary>
    public int Order { get; set; }
    
    /// <summary>
    /// Section entries/data
    /// </summary>
    public List<ReportEntryDto> Entries { get; set; } = new();
}

/// <summary>
/// DTO for a report entry (used for structured reports)
/// </summary>
public class ReportEntryDto
{
    /// <summary>
    /// Entry label/key
    /// </summary>
    public string Label { get; set; } = null!;
    
    /// <summary>
    /// Entry value
    /// </summary>
    public string Value { get; set; } = null!;
    
    /// <summary>
    /// Entry type (text, number, date, etc.)
    /// </summary>
    public string? Type { get; set; }
    
    /// <summary>
    /// Entry order/sequence within section
    /// </summary>
    public int Order { get; set; }
}
