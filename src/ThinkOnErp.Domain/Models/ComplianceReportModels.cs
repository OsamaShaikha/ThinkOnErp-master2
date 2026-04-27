namespace ThinkOnErp.Domain.Models;

/// <summary>
/// Base interface for all compliance reports.
/// Provides common properties and metadata for report generation and export.
/// </summary>
public interface IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    string ReportId { get; set; }
    
    /// <summary>
    /// Type of compliance report (GDPR, SOX, ISO27001, UserActivity, DataModification)
    /// </summary>
    string ReportType { get; set; }
    
    /// <summary>
    /// Title of the report
    /// </summary>
    string Title { get; set; }
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    DateTime GeneratedAt { get; set; }
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period (if applicable)
    /// </summary>
    DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (if applicable)
    /// </summary>
    DateTime? PeriodEndDate { get; set; }
}

/// <summary>
/// GDPR data access report showing all access to a specific data subject's personal data.
/// Supports GDPR Article 15 (Right of Access) compliance requirements.
/// </summary>
public class GdprAccessReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "GDPR_Access";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "GDPR Data Access Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// The data subject (user) whose data access is being reported
    /// </summary>
    public long DataSubjectId { get; set; }
    
    /// <summary>
    /// Name of the data subject
    /// </summary>
    public string DataSubjectName { get; set; } = null!;
    
    /// <summary>
    /// Email of the data subject
    /// </summary>
    public string? DataSubjectEmail { get; set; }
    
    /// <summary>
    /// Total number of access events
    /// </summary>
    public int TotalAccessEvents { get; set; }
    
    /// <summary>
    /// List of all access events to the data subject's personal data
    /// </summary>
    public List<DataAccessEvent> AccessEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of access by entity type
    /// </summary>
    public Dictionary<string, int> AccessByEntityType { get; set; } = new();
    
    /// <summary>
    /// Summary of access by actor
    /// </summary>
    public Dictionary<string, int> AccessByActor { get; set; } = new();
}

/// <summary>
/// Represents a single data access event in a GDPR access report
/// </summary>
public class DataAccessEvent
{
    /// <summary>
    /// When the access occurred
    /// </summary>
    public DateTime AccessedAt { get; set; }
    
    /// <summary>
    /// User ID who accessed the data
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who accessed the data
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Type of entity accessed
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity accessed
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Action performed (READ, UPDATE, DELETE)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Purpose of the access (if recorded)
    /// </summary>
    public string? Purpose { get; set; }
    
    /// <summary>
    /// Legal basis for the access (if recorded)
    /// </summary>
    public string? LegalBasis { get; set; }
    
    /// <summary>
    /// IP address from which the access occurred
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// GDPR data export report containing all personal data for a specific data subject.
/// Supports GDPR Article 20 (Right to Data Portability) compliance requirements.
/// </summary>
public class GdprDataExportReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "GDPR_DataExport";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "GDPR Data Export Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period (not applicable for data export)
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (not applicable for data export)
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// The data subject (user) whose data is being exported
    /// </summary>
    public long DataSubjectId { get; set; }
    
    /// <summary>
    /// Name of the data subject
    /// </summary>
    public string DataSubjectName { get; set; } = null!;
    
    /// <summary>
    /// Email of the data subject
    /// </summary>
    public string? DataSubjectEmail { get; set; }
    
    /// <summary>
    /// All personal data organized by entity type
    /// Key: Entity type (e.g., "SysUser", "SysCompany")
    /// Value: JSON representation of the entity data
    /// </summary>
    public Dictionary<string, List<string>> PersonalDataByEntityType { get; set; } = new();
    
    /// <summary>
    /// Total number of data records exported
    /// </summary>
    public int TotalRecords { get; set; }
    
    /// <summary>
    /// Data categories included in the export
    /// </summary>
    public List<string> DataCategories { get; set; } = new();
}

/// <summary>
/// SOX financial access report showing all access to financial data.
/// Supports SOX Section 404 (Internal Controls) compliance requirements.
/// </summary>
public class SoxFinancialAccessReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "SOX_FinancialAccess";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "SOX Financial Access Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// Total number of financial data access events
    /// </summary>
    public int TotalAccessEvents { get; set; }
    
    /// <summary>
    /// Number of access events outside normal business hours
    /// </summary>
    public int OutOfHoursAccessEvents { get; set; }
    
    /// <summary>
    /// List of all financial data access events
    /// </summary>
    public List<FinancialAccessEvent> AccessEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of access by user
    /// </summary>
    public Dictionary<string, int> AccessByUser { get; set; } = new();
    
    /// <summary>
    /// Summary of access by financial entity type
    /// </summary>
    public Dictionary<string, int> AccessByEntityType { get; set; } = new();
    
    /// <summary>
    /// List of suspicious access patterns detected
    /// </summary>
    public List<string> SuspiciousPatterns { get; set; } = new();
}

/// <summary>
/// Represents a single financial data access event in a SOX report
/// </summary>
public class FinancialAccessEvent
{
    /// <summary>
    /// When the access occurred
    /// </summary>
    public DateTime AccessedAt { get; set; }
    
    /// <summary>
    /// User ID who accessed the financial data
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who accessed the data
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Role of the user at the time of access
    /// </summary>
    public string? ActorRole { get; set; }
    
    /// <summary>
    /// Type of financial entity accessed
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the financial entity accessed
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Action performed (READ, UPDATE, DELETE, EXPORT)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Business justification for the access (if recorded)
    /// </summary>
    public string? BusinessJustification { get; set; }
    
    /// <summary>
    /// Whether the access occurred outside normal business hours
    /// </summary>
    public bool OutOfHours { get; set; }
    
    /// <summary>
    /// IP address from which the access occurred
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// SOX segregation of duties report analyzing role and permission assignments.
/// Supports SOX Section 404 (Internal Controls) compliance requirements.
/// </summary>
public class SoxSegregationOfDutiesReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "SOX_SegregationOfDuties";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "SOX Segregation of Duties Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period (not applicable for segregation analysis)
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (not applicable for segregation analysis)
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// Total number of users analyzed
    /// </summary>
    public int TotalUsersAnalyzed { get; set; }
    
    /// <summary>
    /// Number of potential segregation of duties violations detected
    /// </summary>
    public int ViolationsDetected { get; set; }
    
    /// <summary>
    /// List of detected segregation of duties violations
    /// </summary>
    public List<SegregationViolation> Violations { get; set; } = new();
    
    /// <summary>
    /// Summary of role conflicts by severity
    /// </summary>
    public Dictionary<string, int> ViolationsBySeverity { get; set; } = new();
}

/// <summary>
/// Represents a segregation of duties violation
/// </summary>
public class SegregationViolation
{
    /// <summary>
    /// User ID with the conflicting roles/permissions
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Name of the user
    /// </summary>
    public string UserName { get; set; } = null!;
    
    /// <summary>
    /// Conflicting role 1
    /// </summary>
    public string Role1 { get; set; } = null!;
    
    /// <summary>
    /// Conflicting role 2
    /// </summary>
    public string Role2 { get; set; } = null!;
    
    /// <summary>
    /// Description of the conflict
    /// </summary>
    public string ConflictDescription { get; set; } = null!;
    
    /// <summary>
    /// Severity of the violation (High, Medium, Low)
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Recommended remediation action
    /// </summary>
    public string? Recommendation { get; set; }
}

/// <summary>
/// ISO 27001 security report showing all security events and incidents.
/// Supports ISO 27001 Annex A.12.4 (Logging and Monitoring) compliance requirements.
/// </summary>
public class Iso27001SecurityReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "ISO27001_Security";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "ISO 27001 Security Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// Total number of security events
    /// </summary>
    public int TotalSecurityEvents { get; set; }
    
    /// <summary>
    /// Number of critical security events
    /// </summary>
    public int CriticalEvents { get; set; }
    
    /// <summary>
    /// Number of failed login attempts
    /// </summary>
    public int FailedLoginAttempts { get; set; }
    
    /// <summary>
    /// Number of unauthorized access attempts
    /// </summary>
    public int UnauthorizedAccessAttempts { get; set; }
    
    /// <summary>
    /// List of all security events
    /// </summary>
    public List<SecurityEvent> SecurityEvents { get; set; } = new();
    
    /// <summary>
    /// Summary of events by severity
    /// </summary>
    public Dictionary<string, int> EventsBySeverity { get; set; } = new();
    
    /// <summary>
    /// Summary of events by type
    /// </summary>
    public Dictionary<string, int> EventsByType { get; set; } = new();
    
    /// <summary>
    /// List of security incidents requiring attention
    /// </summary>
    public List<string> IncidentsRequiringAttention { get; set; } = new();
}

/// <summary>
/// Represents a single security event in an ISO 27001 report
/// </summary>
public class SecurityEvent
{
    /// <summary>
    /// When the security event occurred
    /// </summary>
    public DateTime OccurredAt { get; set; }
    
    /// <summary>
    /// Type of security event
    /// </summary>
    public string EventType { get; set; } = null!;
    
    /// <summary>
    /// Severity of the event (Critical, High, Medium, Low)
    /// </summary>
    public string Severity { get; set; } = null!;
    
    /// <summary>
    /// Description of the security event
    /// </summary>
    public string Description { get; set; } = null!;
    
    /// <summary>
    /// User ID associated with the event (if applicable)
    /// </summary>
    public long? UserId { get; set; }
    
    /// <summary>
    /// User name associated with the event (if applicable)
    /// </summary>
    public string? UserName { get; set; }
    
    /// <summary>
    /// IP address associated with the event
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Action taken in response to the event
    /// </summary>
    public string? ActionTaken { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// User activity report showing all actions performed by a specific user.
/// Useful for user behavior analysis, compliance audits, and security investigations.
/// </summary>
public class UserActivityReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "UserActivity";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "User Activity Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// The user whose activity is being reported
    /// </summary>
    public long UserId { get; set; }
    
    /// <summary>
    /// Name of the user
    /// </summary>
    public string UserName { get; set; } = null!;
    
    /// <summary>
    /// Email of the user
    /// </summary>
    public string? UserEmail { get; set; }
    
    /// <summary>
    /// Total number of actions performed
    /// </summary>
    public int TotalActions { get; set; }
    
    /// <summary>
    /// List of all user actions in chronological order
    /// </summary>
    public List<UserActivityAction> Actions { get; set; } = new();
    
    /// <summary>
    /// Summary of actions by type
    /// </summary>
    public Dictionary<string, int> ActionsByType { get; set; } = new();
    
    /// <summary>
    /// Summary of actions by entity type
    /// </summary>
    public Dictionary<string, int> ActionsByEntityType { get; set; } = new();
}

/// <summary>
/// Represents a single user action in a user activity report
/// </summary>
public class UserActivityAction
{
    /// <summary>
    /// When the action occurred
    /// </summary>
    public DateTime PerformedAt { get; set; }
    
    /// <summary>
    /// Type of action (INSERT, UPDATE, DELETE, LOGIN, LOGOUT, etc.)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// Type of entity affected
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity affected
    /// </summary>
    public long? EntityId { get; set; }
    
    /// <summary>
    /// Description of the action
    /// </summary>
    public string? Description { get; set; }
    
    /// <summary>
    /// IP address from which the action was performed
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Data modification report showing all changes to a specific entity.
/// Useful for data lineage tracking, compliance audits, and debugging.
/// </summary>
public class DataModificationReport : IReport
{
    /// <summary>
    /// Unique identifier for the report
    /// </summary>
    public string ReportId { get; set; } = Guid.NewGuid().ToString();
    
    /// <summary>
    /// Type of compliance report
    /// </summary>
    public string ReportType { get; set; } = "DataModification";
    
    /// <summary>
    /// Title of the report
    /// </summary>
    public string Title { get; set; } = "Data Modification Report";
    
    /// <summary>
    /// When the report was generated
    /// </summary>
    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    
    /// <summary>
    /// User ID who generated the report
    /// </summary>
    public long? GeneratedByUserId { get; set; }
    
    /// <summary>
    /// Start date of the report period (not applicable for entity history)
    /// </summary>
    public DateTime? PeriodStartDate { get; set; }
    
    /// <summary>
    /// End date of the report period (not applicable for entity history)
    /// </summary>
    public DateTime? PeriodEndDate { get; set; }
    
    /// <summary>
    /// Type of entity being reported
    /// </summary>
    public string EntityType { get; set; } = null!;
    
    /// <summary>
    /// ID of the entity being reported
    /// </summary>
    public long EntityId { get; set; }
    
    /// <summary>
    /// Total number of modifications
    /// </summary>
    public int TotalModifications { get; set; }
    
    /// <summary>
    /// List of all modifications in chronological order
    /// </summary>
    public List<DataModification> Modifications { get; set; } = new();
    
    /// <summary>
    /// Summary of modifications by action type
    /// </summary>
    public Dictionary<string, int> ModificationsByAction { get; set; } = new();
    
    /// <summary>
    /// Summary of modifications by user
    /// </summary>
    public Dictionary<string, int> ModificationsByUser { get; set; } = new();
}

/// <summary>
/// Represents a single data modification in a data modification report
/// </summary>
public class DataModification
{
    /// <summary>
    /// When the modification occurred
    /// </summary>
    public DateTime ModifiedAt { get; set; }
    
    /// <summary>
    /// Type of modification (INSERT, UPDATE, DELETE)
    /// </summary>
    public string Action { get; set; } = null!;
    
    /// <summary>
    /// User ID who performed the modification
    /// </summary>
    public long ActorId { get; set; }
    
    /// <summary>
    /// Name of the user who performed the modification
    /// </summary>
    public string ActorName { get; set; } = null!;
    
    /// <summary>
    /// Old value before modification (JSON format)
    /// </summary>
    public string? OldValue { get; set; }
    
    /// <summary>
    /// New value after modification (JSON format)
    /// </summary>
    public string? NewValue { get; set; }
    
    /// <summary>
    /// Fields that were changed (for UPDATE operations)
    /// </summary>
    public List<string>? ChangedFields { get; set; }
    
    /// <summary>
    /// IP address from which the modification was performed
    /// </summary>
    public string? IpAddress { get; set; }
    
    /// <summary>
    /// Correlation ID for request tracing
    /// </summary>
    public string? CorrelationId { get; set; }
}

/// <summary>
/// Configuration for scheduled report generation
/// </summary>
public class ReportSchedule
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
    public ReportFrequency Frequency { get; set; }
    
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
    public ReportExportFormat ExportFormat { get; set; }
    
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
/// Report generation frequency options
/// </summary>
public enum ReportFrequency
{
    /// <summary>
    /// Generate report daily
    /// </summary>
    Daily,
    
    /// <summary>
    /// Generate report weekly
    /// </summary>
    Weekly,
    
    /// <summary>
    /// Generate report monthly
    /// </summary>
    Monthly
}

/// <summary>
/// Report export format options
/// </summary>
public enum ReportExportFormat
{
    /// <summary>
    /// Export as PDF document
    /// </summary>
    PDF,
    
    /// <summary>
    /// Export as CSV file
    /// </summary>
    CSV,
    
    /// <summary>
    /// Export as JSON document
    /// </summary>
    JSON
}
